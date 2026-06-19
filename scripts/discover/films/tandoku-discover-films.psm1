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

Export-ModuleMember -Function Get-WikidataUserAgent, Invoke-WikidataSparql, ConvertTo-WikidataQid, Format-FilmEntry, Get-DisplayTitle, Read-FilmsDatabase, Add-Origin
