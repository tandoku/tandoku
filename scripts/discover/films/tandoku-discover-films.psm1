Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"

# Shared helpers for the discover/films workflow scripts.

# User-Agent sent with every request to Wikidata (SPARQL endpoint and Action API).
$script:WikidataUserAgent = 'tandoku-discover/1.0 (https://github.com/tandoku)'

# Canonical key order for film entries in films.yaml. Scripts that rewrite the
# database reorder each entry to this order (unknown keys are appended as-is).
$script:FilmFieldOrder = @('wikidata', 'title', 'type', 'country', 'language', 'year', 'imdb', 'myAnimeList', 'tmdb', 'natively', 'availability', 'origin')

function Get-WikidataUserAgent {
    return $script:WikidataUserAgent
}

# Runs a SPARQL query against the Wikidata Query Service and returns the result
# bindings. Retries on HTTP 429 (rate limiting), honoring the server-provided
# retry delay when present.
function Invoke-WikidataSparql($query) {
    $headers = @{ 'User-Agent' = $script:WikidataUserAgent }
    $url = "https://query.wikidata.org/sparql?query=$([uri]::EscapeDataString($query))&format=json"
    $maxRetries = 3
    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
        try {
            return (Invoke-RestMethod -Uri $url -Headers $headers).results.bindings
        }
        catch {
            $retrySeconds = 0
            if ($_.Exception.Response.StatusCode -eq 429 -or $_ -match '429') {
                if ($_ -match 'retry in (\d+) seconds') {
                    $retrySeconds = [int]$Matches[1]
                } else {
                    $retrySeconds = 120
                }
            }

            if ($retrySeconds -gt 0 -and $attempt -lt $maxRetries) {
                Write-Warning "Rate limited (429) - waiting $retrySeconds seconds before retry $attempt/$maxRetries..."
                Start-Sleep -Seconds $retrySeconds
            } else {
                throw
            }
        }
    }
}

# Extracts the bare QID (e.g. Q42) from a Wikidata entity URI binding value.
function ConvertTo-WikidataQid([string]$entityUri) {
    return $entityUri -replace '.*/entity/', ''
}

# Reorders a film entry's keys to the canonical field order, appending any keys
# not in the canonical order in their existing order.
function Format-FilmEntry($film) {
    $ordered = [ordered]@{}
    foreach ($key in $script:FilmFieldOrder) {
        if ($film.Contains($key)) {
            $ordered[$key] = $film[$key]
        }
    }
    foreach ($key in @($film.Keys)) {
        if (-not $ordered.Contains($key)) {
            $ordered[$key] = $film[$key]
        }
    }
    return $ordered
}

# Adds an origin tag to a film entry's `origin` list (creating it if needed),
# keeping the list de-duplicated and sorted.
function Add-Origin($film, [string]$origin) {
    $existing = @()
    if ($film.Contains('origin') -and $film['origin']) {
        $existing = @($film['origin'])
    }
    if ($existing -notcontains $origin) {
        $existing += $origin
    }
    $film['origin'] = @($existing | Sort-Object)
}

# Removes an origin tag from a film entry's `origin` list, dropping the `origin`
# key entirely once no origins remain.
function Remove-Origin($film, [string]$origin) {
    if (-not ($film.Contains('origin') -and $film['origin'])) {
        return
    }
    $remaining = @(@($film['origin']) | Where-Object { $_ -ne $origin } | Sort-Object)
    if ($remaining.Count -eq 0) {
        $film.Remove('origin')
    } else {
        $film['origin'] = $remaining
    }
}

# Title is a per-language dictionary (e.g. title.en, title.ja); prefer the English
# title for display/search, falling back to any available language.
function Get-DisplayTitle($film) {
    if (-not $film.title) { return $null }
    if ($film.title.en) { return $film.title.en }
    foreach ($value in $film.title.Values) {
        if ($value) { return $value }
    }
    return $null
}

# Reads the films database (a stream of YAML documents) into a mutable list.
# With -AllowMissing, a non-existent database yields an empty list instead of
# throwing (used when a script may be creating the database for the first time).
function Read-FilmsDatabase {
    param(
        [Parameter(Mandatory)]
        [string]$LiteralPath,

        [switch]$AllowMissing
    )

    $films = [System.Collections.Generic.List[object]]::new()
    if ($AllowMissing -and -not (Test-Path -LiteralPath $LiteralPath)) {
        return , $films
    }
    foreach ($doc in @(Import-Yaml -LiteralPath $LiteralPath)) {
        $films.Add($doc)
    }
    return , $films
}

# Registry of the external sources ("origins") a film record can come from. Each
# origin "owns" exactly one external identifier (a path within the film record)
# that uniquely identifies the work for that source and is mirrored on Wikidata
# under a known property. This drives origin-aware Wikidata lookup and the rule
# that an origin-owned identifier is authoritative and must never be silently
# overwritten from Wikidata. Add a new origin here to extend support (e.g. a
# future tmdb or myAnimeList origin) rather than special-casing it in scripts.
$script:FilmOriginRegistry = @(
    [pscustomobject]@{
        Origin        = 'netflix'
        # Wikidata property mirroring this identifier (P1874 = Netflix ID).
        WikidataProp  = 'P1874'
        # Alias used for this identifier in PopulateWikidata's details query.
        WikidataField = 'netflixId'
        # Reads the identifier value from a film record (or $null when absent).
        GetId         = {
            param($film)
            if ($film.Contains('availability') -and $film['availability'] -and
                $film['availability'].Contains('netflix') -and $film['availability']['netflix']) {
                return $film['availability']['netflix']['id']
            }
            return $null
        }
    },
    [pscustomobject]@{
        Origin        = 'imdb'
        # P345 = IMDb ID.
        WikidataProp  = 'P345'
        WikidataField = 'imdbId'
        GetId         = {
            param($film)
            if ($film.Contains('imdb') -and $film['imdb']) {
                return $film['imdb']['id']
            }
            return $null
        }
    }
)

# Returns the origin registry (see $script:FilmOriginRegistry).
function Get-FilmOriginRegistry {
    return $script:FilmOriginRegistry
}

# Returns the registry entry for a given origin name, or $null when the origin
# is not in the registry.
function Get-FilmOriginInfo([string]$origin) {
    foreach ($entry in $script:FilmOriginRegistry) {
        if ($entry.Origin -eq $origin) { return $entry }
    }
    return $null
}

# Reads the identifier value an origin owns from a film record (regardless of
# whether the origin is listed in the record's `origin` field). Returns $null
# when the origin is unknown or the identifier is absent.
function Get-FilmIdentifier($film, [string]$origin) {
    $info = Get-FilmOriginInfo $origin
    if (-not $info) { return $null }
    return (& $info.GetId $film)
}

# Returns the set of identifiers a film record actually owns: one entry per
# registry origin that is listed in the record's `origin` field AND has a
# non-null identifier value. Each entry has Origin, WikidataProp, WikidataField
# and Id.
function Get-FilmOwnedIdentifiers($film) {
    $origins = @()
    if ($film.Contains('origin') -and $film['origin']) {
        $origins = @($film['origin'])
    }
    $owned = [System.Collections.Generic.List[object]]::new()
    foreach ($entry in $script:FilmOriginRegistry) {
        if ($origins -notcontains $entry.Origin) { continue }
        $id = & $entry.GetId $film
        if ($null -eq $id -or $id -eq '') { continue }
        $owned.Add([pscustomobject]@{
            Origin        = $entry.Origin
            WikidataProp  = $entry.WikidataProp
            WikidataField = $entry.WikidataField
            Id            = $id
        })
    }
    return $owned
}

# Deep-merges the $secondary film record into $primary (modifying $primary in
# place) when they represent the same work (e.g. one created from a Netflix
# import and another from an IMDb list import that resolve to the same Wikidata
# entity). Unions origins and `imdb.lists`, and fills any field present on
# $secondary but missing on $primary.
#
# Returns $true on success. If the two records carry conflicting values for any
# origin-owned identifier (e.g. different netflix.id or imdb.id), no merge is
# performed and $false is returned after writing a non-terminating error, so the
# caller can leave both records in place for manual resolution.
function Merge-FilmRecords($primary, $secondary) {
    # Detect conflicting owned identifiers before mutating anything.
    foreach ($entry in $script:FilmOriginRegistry) {
        $a = & $entry.GetId $primary
        $b = & $entry.GetId $secondary
        if ($null -ne $a -and $a -ne '' -and $null -ne $b -and $b -ne '' -and "$a" -ne "$b") {
            Write-Error "Cannot merge film records: conflicting $($entry.Origin) identifier ('$a' vs '$b'). Leaving records unmerged."
            return $false
        }
    }

    foreach ($key in @($secondary.Keys)) {
        if ($key -eq 'origin') {
            foreach ($origin in @($secondary['origin'])) {
                Add-Origin $primary $origin
            }
            continue
        }

        if (-not $primary.Contains($key) -or $null -eq $primary[$key]) {
            # Field only on the secondary record - take it as-is.
            $primary[$key] = $secondary[$key]
            continue
        }

        # Both records have the key; merge dictionaries field-by-field, otherwise
        # keep the primary's value (Wikidata-derived fields are refreshed later).
        if ($primary[$key] -is [System.Collections.IDictionary] -and $secondary[$key] -is [System.Collections.IDictionary]) {
            Merge-FilmSubDictionary $primary[$key] $secondary[$key]
        }
    }

    return $true
}

# Helper for Merge-FilmRecords: merges $secondary dictionary into $primary,
# filling missing keys and unioning nested `lists`/dictionaries.
function Merge-FilmSubDictionary($primary, $secondary) {
    foreach ($key in @($secondary.Keys)) {
        if (-not $primary.Contains($key) -or $null -eq $primary[$key]) {
            $primary[$key] = $secondary[$key]
        }
        elseif ($primary[$key] -is [System.Collections.IDictionary] -and $secondary[$key] -is [System.Collections.IDictionary]) {
            # e.g. imdb.lists or availability.netflix - union nested entries,
            # preferring the primary's value on overlap.
            Merge-FilmSubDictionary $primary[$key] $secondary[$key]
        }
    }
}

Export-ModuleMember -Function Get-WikidataUserAgent, Invoke-WikidataSparql, ConvertTo-WikidataQid, Format-FilmEntry, Get-DisplayTitle, Read-FilmsDatabase, Add-Origin, Remove-Origin, Get-FilmOriginRegistry, Get-FilmOriginInfo, Get-FilmIdentifier, Get-FilmOwnedIdentifiers, Merge-FilmRecords
