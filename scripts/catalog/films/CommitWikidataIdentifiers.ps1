[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$CandidatesPath,

    [Parameter()]
    [string]$AccessToken,

    [Parameter()]
    [string]$ApiUrl = 'https://www.wikidata.org/w/api.php',

    [Parameter()]
    [switch]$Prune
)

Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"
Import-Module "$PSScriptRoot/tandoku-discover-films.psm1"

# Wikidata properties for the external identifiers we commit
$netflixProperty = 'P1874'
$imdbProperty = 'P345'

$userAgent = Get-WikidataUserAgent

# OAuth 2.0 owner-only access token - parameter takes precedence, otherwise environment variable.
# Reading existing claims is public, so a token is only required when actually writing.
$token = if ($AccessToken) { $AccessToken } else { $env:WIKIDATA_ACCESS_TOKEN }
if (-not $WhatIfPreference -and -not $token) {
    throw "Wikidata access token required. Pass -AccessToken or set WIKIDATA_ACCESS_TOKEN (or use -WhatIf to preview)."
}

# Reads (wbgetclaims) are public, so they must never send the access token -
# an invalid/mismatched token would otherwise break reads (and -WhatIf).
$readHeaders = @{ 'User-Agent' = $userAgent }
$writeHeaders = @{ 'User-Agent' = $userAgent }
if ($token) {
    $writeHeaders['Authorization'] = "Bearer $token"
}

$script:csrfToken = $null
function Get-CsrfToken {
    if (-not $script:csrfToken) {
        $body = @{ action = 'query'; meta = 'tokens'; type = 'csrf'; format = 'json' }
        $resp = Invoke-RestMethod -Uri $ApiUrl -Method Post -Body $body -Headers $writeHeaders
        $script:csrfToken = $resp.query.tokens.csrftoken
        if (-not $script:csrfToken -or $script:csrfToken -eq '+\') {
            throw "Failed to obtain a CSRF token (the access token may be invalid or lack edit rights)."
        }
    }
    return $script:csrfToken
}

# Returns the existing values of a string/external-id property on an entity (empty list if none).
function Get-ClaimValues([string]$qid, [string]$property) {
    $uri = "$ApiUrl`?action=wbgetclaims&entity=$qid&property=$property&format=json"
    $resp = Invoke-RestMethod -Uri $uri -Headers $readHeaders
    if ($resp.error) {
        throw "wbgetclaims failed for $qid/$property`: $($resp.error.code) - $($resp.error.info)"
    }
    $values = [System.Collections.Generic.List[string]]::new()
    if ($resp.claims) {
        foreach ($claim in @($resp.claims.$property)) {
            if ($null -eq $claim) { continue }
            $v = $claim.mainsnak.datavalue.value
            if ($null -ne $v) { $values.Add([string]$v) }
        }
    }
    return $values
}

# Finds the Wikidata entity carrying a given IMDb ID (P345), or $null when there is no
# (or more than one) match. Reads are public, so the SPARQL endpoint is queried unauthenticated.
function Find-WikidataIdByImdbId([string]$imdbId) {
    $query = "SELECT ?item WHERE { ?item wdt:P345 `"$imdbId`" . }"
    $bindings = @(Invoke-WikidataSparql $query)
    $qids = @($bindings | ForEach-Object { ConvertTo-WikidataQid $_.item.value } | Sort-Object -Unique)
    if ($qids.Count -eq 0) {
        return $null
    }
    if ($qids.Count -gt 1) {
        Write-Warning "IMDb ID $imdbId matches multiple Wikidata entities [$($qids -join ', ')]; cannot resolve unambiguously"
        return $null
    }
    return $qids[0]
}

# Adds a string/external-id claim, retrying on Wikidata replication lag.
function Add-StringClaim([string]$qid, [string]$property, [string]$value) {
    $body = @{
        action   = 'wbcreateclaim'
        entity   = $qid
        snaktype = 'value'
        property = $property
        value    = (ConvertTo-Json $value -Compress)
        token    = (Get-CsrfToken)
        assert   = 'user'
        maxlag   = 5
        summary  = "tandoku: add $property identifier"
        format   = 'json'
    }

    $maxRetries = 3
    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
        $resp = Invoke-RestMethod -Uri $ApiUrl -Method Post -Body $body -Headers $writeHeaders
        if ($resp.success -eq 1) {
            return $true
        }
        if ($resp.error -and $resp.error.code -eq 'maxlag' -and $attempt -lt $maxRetries) {
            Write-Warning "Wikidata lagged; waiting 5s before retry $attempt/$maxRetries..."
            Start-Sleep -Seconds 5
            continue
        }
        $err = if ($resp.error) { "$($resp.error.code) - $($resp.error.info)" } else { 'unknown error' }
        Write-Warning "Failed to add $property=$value to $qid`: $err"
        return $false
    }
    return $false
}

# --- Read candidates ---

$candidates = @(Import-Yaml $CandidatesPath)
Write-Host "Read $($candidates.Count) candidate records from $CandidatesPath"

$verifiedCount = 0
$skipped = 0
$upToDate = 0
$claimsAdded = 0
$upToDateRecords = [System.Collections.Generic.List[object]]::new()

foreach ($record in $candidates) {
    if ($record.verified -ne $true) {
        continue
    }
    $verifiedCount++

    $netflixLabel = if ($record.netflix) { $record.netflix.id } else { '(no netflix id)' }

    # Netflix ID (optional)
    $netflixId = $null
    if ($record.netflix -and $null -ne $record.netflix.id) {
        $netflixId = [string]$record.netflix.id
        if ($netflixId -notmatch '^[0-9]+$') {
            Write-Warning "Netflix $netflixLabel has invalid netflix.id '$netflixId'; skipping"
            $skipped++
            continue
        }
    }

    # IMDb candidates - a single entry is required to commit an IMDb ID.
    # Assign directly (not via an if-expression) so a single-element array is not unwrapped.
    [object[]]$imdbEntries = @()
    if ($null -ne $record.imdb) {
        $imdbEntries = @($record.imdb)
    }
    if ($imdbEntries.Count -gt 1) {
        Write-Warning "Netflix $netflixLabel has $($imdbEntries.Count) IMDb candidates; remove the incorrect entries first. Skipping."
        $skipped++
        continue
    }
    $imdbId = $null
    if ($imdbEntries.Count -eq 1) {
        $imdbId = [string]$imdbEntries[0].id
        if ($imdbId -notmatch '^tt[0-9]+$') {
            Write-Warning "Netflix $netflixLabel has invalid imdb.id '$imdbId'; skipping"
            $skipped++
            continue
        }
    }

    if (-not $netflixId -and -not $imdbId) {
        Write-Warning "Netflix $netflixLabel has neither netflix.id nor imdb.id; skipping"
        $skipped++
        continue
    }

    # Resolve the Wikidata entity. A record may omit wikidata.id; in that case fall back to
    # locating the entity by its IMDb ID (P345) so verified records can still be committed.
    $qid = if ($record.wikidata) { [string]$record.wikidata.id } else { $null }
    if (-not $qid) {
        if (-not $imdbId) {
            Write-Warning "Netflix $netflixLabel is verified but has no wikidata.id and no imdb.id to search by; skipping"
            $skipped++
            continue
        }
        try {
            $qid = Find-WikidataIdByImdbId $imdbId
        }
        catch {
            Write-Warning "Failed to search Wikidata for IMDb ID $imdbId (Netflix $netflixLabel): $_. Skipping."
            $skipped++
            continue
        }
        if (-not $qid) {
            Write-Warning "Netflix $netflixLabel is verified but has no wikidata.id and no Wikidata entity was found for IMDb ID $imdbId; skipping"
            $skipped++
            continue
        }
        Write-Host "Netflix ${netflixLabel}: resolved wikidata.id $qid from IMDb ID $imdbId"
    }
    if ($qid -notmatch '^Q[1-9][0-9]*$') {
        Write-Warning "Netflix $netflixLabel has invalid wikidata.id '$qid'; skipping"
        $skipped++
        continue
    }

    try {
        $existingImdb = @(Get-ClaimValues $qid $imdbProperty)
        $existingNetflix = @(Get-ClaimValues $qid $netflixProperty)
    }
    catch {
        Write-Warning "Failed to read existing claims for $qid`: $_. Skipping."
        $skipped++
        continue
    }

    # A pre-existing, non-matching external ID means this wikidata.id mapping is suspect;
    # make no change to the record at all.
    if ($imdbId -and $existingImdb.Count -gt 0 -and $existingImdb -notcontains $imdbId) {
        Write-Warning "$qid already has IMDb ID(s) [$($existingImdb -join ', ')] that do not match candidate '$imdbId'; skipping"
        $skipped++
        continue
    }
    if ($netflixId -and $existingNetflix.Count -gt 0 -and $existingNetflix -notcontains $netflixId) {
        Write-Warning "$qid already has Netflix ID(s) [$($existingNetflix -join ', ')] that do not match candidate '$netflixId'; skipping"
        $skipped++
        continue
    }

    # Determine which claims are missing (idempotent - never duplicate an existing value)
    $toAdd = [System.Collections.Generic.List[object]]::new()
    if ($netflixId -and $existingNetflix -notcontains $netflixId) {
        $toAdd.Add(@{ property = $netflixProperty; value = $netflixId })
    }
    if ($imdbId -and $existingImdb -notcontains $imdbId) {
        $toAdd.Add(@{ property = $imdbProperty; value = $imdbId })
    }

    if ($toAdd.Count -eq 0) {
        Write-Host "$qid already up to date"
        $upToDate++
        $upToDateRecords.Add($record)
        continue
    }

    if (-not $imdbId) {
        Write-Host "$qid has no IMDb candidate; committing Netflix ID only"
    }

    foreach ($claim in $toAdd) {
        if ($PSCmdlet.ShouldProcess($qid, "add $($claim.property) = $($claim.value)")) {
            if (Add-StringClaim $qid $claim.property $claim.value) {
                Write-Host "$qid`: added $($claim.property) = $($claim.value)"
                $claimsAdded++
            }
        }
    }
}

Write-Host "Done - $verifiedCount verified record(s): added $claimsAdded claim(s), $upToDate already up to date, $skipped skipped"

# Optionally remove already-up-to-date entries from the candidates file so subsequent runs only
# revisit records that still need work.
if ($Prune) {
    if ($upToDateRecords.Count -eq 0) {
        Write-Host "Prune: no already-up-to-date entries to remove"
    }
    elseif ($PSCmdlet.ShouldProcess($CandidatesPath, "remove $($upToDateRecords.Count) already-up-to-date entry(ies)")) {
        $remaining = @($candidates | Where-Object { $upToDateRecords -notcontains $_ })
        $remaining | Export-Yaml -Path $CandidatesPath
        Write-Host "Pruned $($upToDateRecords.Count) already-up-to-date entry(ies); $($remaining.Count) record(s) remain in $CandidatesPath"
    }
}
