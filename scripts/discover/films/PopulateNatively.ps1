[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath
)

Import-Module "$PSScriptRoot\..\..\modules\tandoku-yaml.psm1"

# Ensure UTF-8 encoding for yq compatibility
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8

# Preferred key order for film entries
$fieldOrder = @('wikidata', 'title', 'title-ja', 'type', 'originCountry', 'originalLanguage', 'year', 'imdb', 'myAnimeList', 'tmdb', 'natively', 'providers')

$nativelyBaseUrl = 'https://learnnatively.com'
$nativelyHeaders = @{
    "User-Agent" = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    "Accept"     = "*/*"
    "Referer"    = "https://learnnatively.com/search/jpn/videos/"
}

function Reorder-FilmEntry($film) {
    $ordered = [ordered]@{}
    foreach ($key in $fieldOrder) {
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

function Search-Natively($query) {
    $encoded = [uri]::EscapeDataString($query)
    $url = "$nativelyBaseUrl/api/ninja/search/videos/?language=jpn&q=$encoded&series=series"
    return Invoke-RestMethod -Uri $url -Headers $nativelyHeaders
}

function Format-NativelyLevel($rating) {
    if (-not $rating -or $null -eq $rating.lvl) { return $null }
    return [int]$rating.lvl
}

# Produce a relaxed version of a title for a fallback search: strips parenthetical
# annotations (half- or full-width) and any subtitle following a colon, e.g.
# 'HUNTER×HUNTER (2011年のアニメ)' -> 'HUNTER×HUNTER' and '愛なき森で叫べ : Deep Cut' -> '愛なき森で叫べ'.
function Get-RelaxedQuery($query) {
    $relaxed = $query -replace '[\(（][^\)）]*[\)）]', ''
    $relaxed = ($relaxed -split '[:：]', 2)[0]
    # Drop zero-width/BOM characters and surrounding whitespace
    $relaxed = ($relaxed -replace '[\uFEFF\u200B]', '').Trim()
    return $relaxed
}

# Search Natively results for an entry whose TMDB ID matches $tmdbId; returns a
# match descriptor (level/temporary/url) or $null if none of the results match.
function Find-NativelyMatch($results, $tmdbId) {
    foreach ($result in $results) {
        if ($result.widget -eq 'series') {
            if ([int]$result.series.tmdb_id -eq [int]$tmdbId) {
                return @{
                    level     = Format-NativelyLevel $result.series.rating
                    temporary = [bool]$result.series.rating.temporary
                    url       = "$nativelyBaseUrl$($result.series.url)"
                }
            }
        } else {
            # 'item' widget - check both item-level and series-level tmdb_id
            if ([int]$result.item.tmdb_id -eq [int]$tmdbId) {
                return @{
                    level     = Format-NativelyLevel $result.item.rating
                    temporary = [bool]$result.item.rating.temporary
                    url       = "$nativelyBaseUrl$($result.item.url)"
                }
            }
            if ($result.item.series -and [int]$result.item.series.tmdb_id -eq [int]$tmdbId) {
                return @{
                    level     = Format-NativelyLevel $result.item.rating
                    temporary = [bool]$result.item.rating.temporary
                    url       = "$nativelyBaseUrl$($result.item.series.url)"
                }
            }
        }
    }
    return $null
}

# Read films database
$films = [System.Collections.Generic.List[object]]::new()
foreach ($doc in @(Import-Yaml -LiteralPath $DatabasePath)) {
    $films.Add($doc)
}

Write-Host "Read $($films.Count) entries from films database"

# Filter to Japanese-language entries that need Natively data.
# An entry needs a (re)lookup when it has no Natively data, or when the captured
# tmdbId/tmdbKind are missing or no longer match the current tmdb info.
$needsNatively = @()
foreach ($film in $films) {
    if ($film.originalLanguage -ne 'ja' -or -not $film.'title-ja' -or -not $film.tmdb) {
        continue
    }
    if (-not $film.natively) {
        $needsNatively += $film
    } elseif (
        [int]$film.natively.tmdbId -ne [int]$film.tmdb.id -or
        $film.natively.tmdbKind -ne $film.tmdb.kind
    ) {
        $needsNatively += $film
    }
}

if ($needsNatively.Count -eq 0) {
    Write-Host "No entries need Natively lookup"
    return
}

Write-Host "$($needsNatively.Count) entries need Natively lookup"

$matched = 0
$notFound = 0

foreach ($film in $needsNatively) {
    $titleJa = $film.'title-ja'
    $tmdbId = $film.tmdb.id

    # Throttle requests
    $delay = 1000 + (Get-Random -Maximum 1000)
    Start-Sleep -Milliseconds $delay

    try {
        $searchResult = Search-Natively $titleJa
    }
    catch {
        Write-Warning "Failed to search Natively for '$titleJa': $_"
        continue
    }

    $results = @($searchResult.results)
    $resultCount = $results.Count

    # Search through results for a TMDB ID match
    $matchedResult = Find-NativelyMatch $results $tmdbId

    # Fall back to a relaxed query (parenthetical/subtitle stripped) if no match
    if (-not ($matchedResult -and $matchedResult.level)) {
        $relaxedQuery = Get-RelaxedQuery $titleJa
        if ($relaxedQuery -and $relaxedQuery -ne $titleJa) {
            $delay = 1000 + (Get-Random -Maximum 1000)
            Start-Sleep -Milliseconds $delay
            try {
                $relaxedSearch = Search-Natively $relaxedQuery
                $relaxedResults = @($relaxedSearch.results)
                $relaxedMatch = Find-NativelyMatch $relaxedResults $tmdbId
                if ($relaxedMatch -and $relaxedMatch.level) {
                    $matchedResult = $relaxedMatch
                    $resultCount = $relaxedResults.Count
                    Write-Host "Relaxed match for '$titleJa' using '$relaxedQuery'"
                }
            }
            catch {
                Write-Warning "Failed relaxed Natively search for '$relaxedQuery': $_"
            }
        }
    }

    if ($matchedResult -and $matchedResult.level) {
        $film['natively'] = [ordered]@{
            level = $matchedResult.level
            url   = $matchedResult.url
        }
        if ($matchedResult.temporary) {
            $film['natively']['temporaryLevel'] = $true
        }
        # Capture the TMDB info that was matched so we can recheck Natively if it changes
        $film['natively']['tmdbId'] = [int]$film.tmdb.id
        $film['natively']['tmdbKind'] = $film.tmdb.kind
        $matched++
        Write-Host "Matched '$titleJa' -> $($matchedResult.url) (level $($matchedResult.level))"
    } else {
        Write-Warning "No Natively match for '$titleJa' (tmdb=$tmdbId kind=$($film.tmdb.kind) wikidata=$($film.wikidata)) - $resultCount search result(s)"
        $notFound++
    }
}

# Reorder keys and write films.yaml
for ($i = 0; $i -lt $films.Count; $i++) {
    $films[$i] = Reorder-FilmEntry $films[$i]
}
$films | Export-Yaml -Path $DatabasePath

Write-Host "Done: $matched matched, $notFound not found - $($films.Count) total entries"
