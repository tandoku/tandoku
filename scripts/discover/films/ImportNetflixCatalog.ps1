[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath,

    [string[]]$Country = @('US'),

    [string]$AudioLanguage = 'ja',

    [string]$SubtitleLanguage,

    [int]$MaxResults = 0,

    [string]$ApiKey = $env:RAPIDAPI_KEY
)

Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"
Import-Module "$PSScriptRoot/tandoku-discover-films.psm1"

if (-not $ApiKey) {
    throw 'A uNoGS RapidAPI key is required. Pass -ApiKey or set the RAPIDAPI_KEY environment variable.'
}

$script:ApiHost = 'unogsng.p.rapidapi.com'
$script:ApiHeaders = @{
    'x-rapidapi-key'  = $ApiKey
    'x-rapidapi-host' = $script:ApiHost
}

# Maps a lower-cased base language name (as returned by Netflix audio/subtitle
# lists, e.g. 'japanese', 'chinese') to its ISO 639 code. Built from .NET's
# neutral cultures so the common languages are covered without a hand-maintained
# table; the first code wins for names shared across cultures.
$script:LanguageCodeByName = @{}
foreach ($ci in [System.Globalization.CultureInfo]::GetCultures([System.Globalization.CultureTypes]::NeutralCultures)) {
    if ($ci.TwoLetterISOLanguageName) {
        $base = ($ci.EnglishName -split ' \(')[0].Trim().ToLowerInvariant()
        if ($base -and -not $script:LanguageCodeByName.ContainsKey($base)) {
            $script:LanguageCodeByName[$base] = $ci.TwoLetterISOLanguageName
        }
    }
}

# Netflix uses a few language names that don't match any .NET culture's English
# name; map them explicitly.
$languageAliases = @{
    'mandarin'  = 'zh'
    'cantonese' = 'zh'
    'flemish'   = 'nl'
    'farsi'     = 'fa'
}
foreach ($alias in $languageAliases.Keys) {
    $script:LanguageCodeByName[$alias] = $languageAliases[$alias]
}

# Calls a uNoGS endpoint, URL-encoding query values and retrying on HTTP 429.
function Invoke-UnogsApi {
    param(
        [Parameter(Mandatory)]
        [string]$Endpoint,

        [hashtable]$Query = @{}
    )

    $pairs = foreach ($key in $Query.Keys) {
        $value = $Query[$key]
        if ($null -ne $value -and $value -ne '') {
            "$key=$([uri]::EscapeDataString([string]$value))"
        }
    }
    $url = "https://$($script:ApiHost)/$Endpoint"
    if ($pairs) {
        $url += '?' + ($pairs -join '&')
    }

    $maxRetries = 5
    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
        try {
            return Invoke-RestMethod -Uri $url -Headers $script:ApiHeaders -Method Get
        }
        catch {
            $status = $_.Exception.Response.StatusCode.value__
            if ($status -eq 429 -and $attempt -lt $maxRetries) {
                $retrySeconds = 2
                $retryAfter = $_.Exception.Response.Headers['Retry-After']
                if ($retryAfter -and [int]::TryParse($retryAfter, [ref]$null)) {
                    $retrySeconds = [int]$retryAfter
                }
                Write-Warning "Rate limited (429) - waiting $retrySeconds seconds before retry $attempt/$maxRetries..."
                Start-Sleep -Seconds $retrySeconds
            } else {
                throw
            }
        }
    }
}

# Converts a two-letter ISO 639-1 code into the Netflix language name used by the
# search endpoint's audio/subtitle filters (e.g. 'ja' -> 'japanese').
function Get-NetflixLanguageName([string]$code) {
    try {
        return [System.Globalization.CultureInfo]::GetCultureInfo($code).EnglishName.ToLowerInvariant()
    }
    catch {
        throw "Unknown language code '$code'."
    }
}

# Resolves a cleaned, lower-cased language name to an ISO 639 code. Netflix is
# inconsistent about region qualifiers (e.g. 'Chinese (Simplified)' vs
# 'Simplified Chinese'), so when a direct match fails we drop leading words and
# match on the trailing language name (e.g. 'simplified chinese' -> 'chinese').
function Resolve-LanguageCode([string]$name) {
    if ($script:LanguageCodeByName.ContainsKey($name)) {
        return $script:LanguageCodeByName[$name]
    }
    $words = $name -split '\s+'
    for ($i = 1; $i -lt $words.Count; $i++) {
        $candidate = ($words[$i..($words.Count - 1)] -join ' ')
        if ($script:LanguageCodeByName.ContainsKey($candidate)) {
            return $script:LanguageCodeByName[$candidate]
        }
    }
    return $null
}

# Parses a Netflix audio/subtitle list (e.g. 'English,Japanese [Original],
# Spanish (Latin America) - Audio Description') into a sorted, de-duplicated list
# of ISO 639 language codes.
function ConvertTo-LanguageCodes([string]$value) {
    $codes = [System.Collections.Generic.HashSet[string]]::new()
    if ($value) {
        foreach ($part in $value -split ',') {
            $name = $part -replace '\s*-\s*Audio Description\s*$', ''
            $name = $name -replace '\s*\[Original\]\s*$', ''
            $name = $name -replace '\s*\(.*\)\s*$', ''
            $name = $name.Trim().ToLowerInvariant()
            if (-not $name) { continue }

            $code = Resolve-LanguageCode $name
            if ($code) {
                [void]$codes.Add($code)
            } else {
                Write-Warning "Unrecognized Netflix language name '$name'"
            }
        }
    }
    return @($codes | Sort-Object)
}

# Builds the availability.netflix record, preserving any extra keys (e.g.
# watchlist) already present on the existing record.
function New-NetflixRecord($existing, $id, $title, $type, $year, $countryDetails) {
    $record = [ordered]@{
        id             = $id
        title          = $title
        type           = $type
        year           = $year
        countryDetails = $countryDetails
    }
    if ($existing) {
        foreach ($key in $existing.Keys) {
            if (-not $record.Contains($key)) {
                $record[$key] = $existing[$key]
            }
        }
    }
    return $record
}

# Resolve the requested country codes to uNoGS numeric country IDs.
$countriesResult = Invoke-UnogsApi -Endpoint 'countries'
$countryIdByCode = @{}
foreach ($entry in $countriesResult.results) {
    $countryIdByCode[$entry.countrycode.ToUpperInvariant()] = $entry.id
}

$countryCodes = @($Country | ForEach-Object { $_.ToUpperInvariant() })
$countryIds = foreach ($code in $countryCodes) {
    if (-not $countryIdByCode.ContainsKey($code)) {
        throw "Unknown country code '$code'. Expected an ISO 3166-1 alpha-2 code available on Netflix (e.g. US, JP)."
    }
    $countryIdByCode[$code]
}

# Build the search filter.
$searchQuery = [ordered]@{
    countrylist         = ($countryIds -join ',')
    country_andorunique = 'or'
    orderby             = 'date'
    limit               = 100
    offset              = 0
}
if ($AudioLanguage) {
    $searchQuery['audio'] = Get-NetflixLanguageName $AudioLanguage
}
if ($SubtitleLanguage) {
    $searchQuery['subtitle'] = Get-NetflixLanguageName $SubtitleLanguage
}
if ($AudioLanguage -and $SubtitleLanguage) {
    $searchQuery['audiosubtitle_andor'] = 'and'
}

$filterParts = @()
if ($AudioLanguage) { $filterParts += "audio=$AudioLanguage" }
if ($SubtitleLanguage) { $filterParts += "subtitle=$SubtitleLanguage" }
$filterDesc = if ($filterParts) { $filterParts -join ', ' } else { '(no language filter)' }
Write-Host "Searching Netflix catalog in $($countryCodes -join ', ') with $filterDesc"

# Page through the search results.
$searchResults = [System.Collections.Generic.List[object]]::new()
$total = $null
while ($true) {
    $response = Invoke-UnogsApi -Endpoint 'search' -Query $searchQuery
    if ($null -eq $total) {
        $total = $response.total
        Write-Host "Found $total matching titles"
    }
    foreach ($result in $response.results) {
        $searchResults.Add($result)
    }
    if ($MaxResults -gt 0 -and $searchResults.Count -ge $MaxResults) {
        break
    }
    $searchQuery['offset'] = [int]$searchQuery['offset'] + [int]$searchQuery['limit']
    if ([int]$searchQuery['offset'] -ge [int]$total) {
        break
    }
}
if ($MaxResults -gt 0 -and $searchResults.Count -gt $MaxResults) {
    $searchResults = [System.Collections.Generic.List[object]]($searchResults | Select-Object -First $MaxResults)
}

# Read existing films database.
$films = Read-FilmsDatabase -LiteralPath $DatabasePath -AllowMissing
Write-Host "Read $($films.Count) existing entries from films database"

# Build lookup table for existing films by Netflix ID.
$filmsByNetflixId = @{}
for ($i = 0; $i -lt $films.Count; $i++) {
    $film = $films[$i]
    if ($film.availability -and $film.availability.netflix -and $null -ne $film.availability.netflix.id) {
        $filmsByNetflixId[[string]$film.availability.netflix.id] = $i
    }
}

# Process each matching title.
$added = 0
$updated = 0
$processed = 0
foreach ($result in $searchResults) {
    $netflixId = [string]$result.nfid
    $processed++
    Write-Host "[$processed/$($searchResults.Count)] $($result.title) ($netflixId)"

    # Retrieve per-country availability details.
    $titleCountries = (Invoke-UnogsApi -Endpoint 'titlecountries' -Query @{ netflixid = $netflixId }).results
    $countryDetails = [ordered]@{}
    foreach ($code in $countryCodes) {
        $detail = $titleCountries | Where-Object { $_.cc -eq $code } | Select-Object -First 1
        if ($detail) {
            $countryDetail = [ordered]@{}
            if ($null -ne $detail.seasdet -and $detail.seasdet -ne '') {
                $countryDetail['seasonDetails'] = $detail.seasdet
            }
            $countryDetail['newDate'] = $detail.newdate
            if ($null -ne $detail.expiredate -and $detail.expiredate -ne '') {
                $countryDetail['expireDate'] = $detail.expiredate
            }
            $countryDetail['audio'] = ConvertTo-LanguageCodes $detail.audio
            $countryDetail['subtitle'] = ConvertTo-LanguageCodes $detail.subtitle
            $countryDetails[$code] = $countryDetail
        }
    }

    if ($filmsByNetflixId.ContainsKey($netflixId)) {
        # Update existing entry, preserving other fields (e.g. watchlist).
        $film = $films[$filmsByNetflixId[$netflixId]]
        $film['availability']['netflix'] = New-NetflixRecord `
            $film['availability']['netflix'] ([int]$result.nfid) $result.title $result.vtype $result.year $countryDetails
        Add-Origin $film 'netflix'
        $updated++
    } else {
        # Add new entry.
        $newFilm = [ordered]@{
            availability = [ordered]@{
                netflix = New-NetflixRecord $null ([int]$result.nfid) $result.title $result.vtype $result.year $countryDetails
            }
        }
        Add-Origin $newFilm 'netflix'
        $films.Add($newFilm)
        $filmsByNetflixId[$netflixId] = $films.Count - 1
        $added++
    }
}

# Write films.yaml
$films | Export-Yaml -Path $DatabasePath

Write-Host "Done: $added added, $updated updated - $($films.Count) total entries"
