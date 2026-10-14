Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"

# Shared helpers for the discover/films workflow scripts.

# User-Agent sent with every request to Wikidata (SPARQL endpoint and Action API).
$script:WikidataUserAgent = 'tandoku-discover/1.0 (https://github.com/tandoku)'

# Canonical V3 key order for film entries in films.yaml. Scripts that rewrite the
# database reorder each entry to this order (unknown keys are appended as-is).
$script:FilmFieldOrder = @('origin', 'identifiers', 'sources')

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

# Prefer Wikidata's English label, then another label, then source titles.
function Get-DisplayTitle($film) {
    $wikidata = Get-FilmSource $film 'wikidata'
    if ($wikidata -and $wikidata.title) {
        if ($wikidata.title.en) { return $wikidata.title.en }
        foreach ($value in $wikidata.title.Values) {
            if ($value) { return $value }
        }
    }
    foreach ($name in @('imdb', 'netflix-unogs', 'netflix-watchlist', 'imdb-lists')) {
        $source = Get-FilmSource $film $name
        if ($source -and $source.title) { return $source.title }
    }
    return $null
}

function Get-NetflixTitle($film) {
    foreach ($name in @('netflix-unogs', 'netflix-watchlist')) {
        $source = Get-FilmSource $film $name
        if ($source -and $source.title) { return $source.title }
    }
    return $null
}

function Get-FilmSource($film, [string]$name, [switch]$Create) {
    if (-not $film.Contains('sources') -or $null -eq $film['sources']) {
        if (-not $Create) { return $null }
        $film['sources'] = [ordered]@{}
    }
    if (-not $film['sources'].Contains($name) -or $null -eq $film['sources'][$name]) {
        if (-not $Create) { return $null }
        $film['sources'][$name] = [ordered]@{}
    }
    return $film['sources'][$name]
}

function Set-FilmIdentifier($film, [string]$name, $id, [string]$kind) {
    if (-not $film.Contains('identifiers') -or $null -eq $film['identifiers']) {
        $film['identifiers'] = [ordered]@{}
    }
    if (-not $film['identifiers'].Contains($name) -or $null -eq $film['identifiers'][$name]) {
        $film['identifiers'][$name] = [ordered]@{}
    }
    $film['identifiers'][$name]['id'] = $id
    if ($kind) { $film['identifiers'][$name]['kind'] = $kind }
}

# Reads the films database (a stream of YAML documents) into a mutable list.
# With -AllowMissing, a non-existent database yields an empty list instead of
# throwing (used when a script may be creating the database for the first time).
function Read-FilmsDatabase {
    param(
        [Parameter(Mandatory)]
        [string]$LiteralPath,

        [switch]$AllowMissing,

        [switch]$AllowLegacy
    )

    $films = [System.Collections.Generic.List[object]]::new()
    if ($AllowMissing -and -not (Test-Path -LiteralPath $LiteralPath)) {
        return , $films
    }
    foreach ($doc in @(Import-Yaml -LiteralPath $LiteralPath)) {
        if ($null -eq $doc) { continue }
        if (-not $AllowLegacy) {
            foreach ($key in @('wikidata', 'title', 'type', 'country', 'language', 'year', 'imdb', 'myAnimeList', 'tmdb', 'natively', 'availability', 'providers', 'title-ja', 'originCountry', 'originalLanguage')) {
                if ($doc.Contains($key)) {
                    throw "Legacy film record in '$LiteralPath'. For original V1 data, run MigrateFilmsDatabase.ps1 (V1 to V2) first. Run MigrateFilmsDatabaseV2.ps1 (V2 to V3) before using the V3 workflow."
                }
            }
            if (@($doc.origin) | Where-Object { $_ -in @('imdb', 'netflix') }) {
                throw "Legacy film origins in '$LiteralPath'. Run MigrateFilmsDatabaseV2.ps1 (V2 to V3) before using the V3 workflow."
            }
        }
        $films.Add($doc)
    }
    return , $films
}

# Import inputs share provider identifiers. An identifier backed by an origin
# remains authoritative when Wikidata supplies cross-references.
$script:FilmOriginRegistry = @(
    [pscustomobject]@{
        Origin        = 'netflix-unogs'
        Identifier    = 'netflix'
        WikidataProp  = 'P1874'
        WikidataField = 'netflixId'
    },
    [pscustomobject]@{
        Origin        = 'netflix-watchlist'
        Identifier    = 'netflix'
        WikidataProp  = 'P1874'
        WikidataField = 'netflixId'
    },
    [pscustomobject]@{
        Origin        = 'imdb-lists'
        Identifier    = 'imdb'
        WikidataProp  = 'P345'
        WikidataField = 'imdbId'
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

# Reads a shared provider identifier, independent of its contributing origins.
function Get-FilmIdentifier($film, [string]$name) {
    if ($film.Contains('identifiers') -and $film['identifiers'] -and
        $film['identifiers'].Contains($name) -and $film['identifiers'][$name]) {
        return $film['identifiers'][$name]['id']
    }
    return $null
}

# Returns the set of identifiers a film record actually owns: one entry per
# provider backed by a listed origin and a non-null identifier. Netflix's two
# input origins contribute the same identifier only once.
function Get-FilmOwnedIdentifiers($film) {
    $origins = @()
    if ($film.Contains('origin') -and $film['origin']) {
        $origins = @($film['origin'])
    }
    $owned = [System.Collections.Generic.List[object]]::new()
    $seen = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($entry in $script:FilmOriginRegistry) {
        if ($origins -notcontains $entry.Origin) { continue }
        $id = Get-FilmIdentifier $film $entry.Identifier
        if ($null -eq $id -or $id -eq '') { continue }
        if (-not $seen.Add($entry.Identifier)) { continue }
        $owned.Add([pscustomobject]@{
            Origin        = $entry.Origin
            Identifier    = $entry.Identifier
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
# entity). Unions origins and source dictionaries, and fills any field present on
# $secondary but missing on $primary.
#
# Returns $true on success. If the two records carry conflicting values for any
# shared identifier (including TMDB kind), no merge is
# performed and $false is returned after writing a non-terminating error, so the
# caller can leave both records in place for manual resolution.
function Merge-FilmRecords {
    [CmdletBinding()]
    param($primary, $secondary)

    if ($primary.identifiers -and $secondary.identifiers) {
        foreach ($name in $secondary.identifiers.Keys) {
            if (-not $primary.identifiers.Contains($name)) { continue }
            foreach ($field in @('id', 'kind')) {
                $a = $primary.identifiers[$name][$field]
                $b = $secondary.identifiers[$name][$field]
                if ($null -ne $a -and $a -ne '' -and $null -ne $b -and $b -ne '' -and "$a" -ne "$b") {
                    Write-Error "Cannot merge film records: conflicting identifiers.$name.$field ('$a' vs '$b'). Leaving records unmerged." -ErrorAction $ErrorActionPreference
                    return $false
                }
            }
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
            # e.g. sources.imdb-lists.lists - union nested entries,
            # preferring the primary's value on overlap.
            Merge-FilmSubDictionary $primary[$key] $secondary[$key]
        }
    }
}

Export-ModuleMember -Function Get-WikidataUserAgent, Invoke-WikidataSparql, ConvertTo-WikidataQid, Format-FilmEntry, Get-DisplayTitle, Get-NetflixTitle, Get-FilmSource, Set-FilmIdentifier, Read-FilmsDatabase, Add-Origin, Remove-Origin, Get-FilmOriginRegistry, Get-FilmOriginInfo, Get-FilmIdentifier, Get-FilmOwnedIdentifiers, Merge-FilmRecords
