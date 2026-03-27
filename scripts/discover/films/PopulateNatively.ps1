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
    if ($rating.temporary) {
        return "$($rating.lvl)??"
    }
    return [int]$rating.lvl
}

# Read films database
$films = [System.Collections.Generic.List[object]]::new()
foreach ($doc in @(Import-Yaml -LiteralPath $DatabasePath)) {
    $films.Add($doc)
}

Write-Host "Read $($films.Count) entries from films database"

# Filter to Japanese-language entries that need Natively data
$needsNatively = @()
foreach ($film in $films) {
    if ($film.originalLanguage -eq 'ja' -and -not $film.natively -and $film.'title-ja' -and $film.tmdb) {
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
    $matchedResult = $null
    foreach ($result in $results) {
        if ($result.widget -eq 'series') {
            if ([int]$result.series.tmdb_id -eq [int]$tmdbId) {
                $matchedResult = @{
                    level = Format-NativelyLevel $result.series.rating
                    url   = "$nativelyBaseUrl$($result.series.url)"
                }
                break
            }
        } else {
            # 'item' widget - check both item-level and series-level tmdb_id
            if ([int]$result.item.tmdb_id -eq [int]$tmdbId) {
                $matchedResult = @{
                    level = Format-NativelyLevel $result.item.rating
                    url   = "$nativelyBaseUrl$($result.item.url)"
                }
                break
            }
            if ($result.item.series -and [int]$result.item.series.tmdb_id -eq [int]$tmdbId) {
                $matchedResult = @{
                    level = Format-NativelyLevel $result.item.rating
                    url   = "$nativelyBaseUrl$($result.item.series.url)"
                }
                break
            }
        }
    }

    if ($matchedResult -and $matchedResult.level) {
        $film['natively'] = [ordered]@{
            level = $matchedResult.level
            url   = $matchedResult.url
        }
        $matched++
        Write-Host "Matched '$titleJa' -> $($matchedResult.url) (level $($matchedResult.level))"
    } else {
        Write-Warning "No Natively match for '$titleJa' (tmdb=$tmdbId) - $resultCount search result(s)"
        $notFound++
    }
}

# Reorder keys and write films.yaml
for ($i = 0; $i -lt $films.Count; $i++) {
    $films[$i] = Reorder-FilmEntry $films[$i]
}
$films | Export-Yaml -Path $DatabasePath

Write-Host "Done: $matched matched, $notFound not found - $($films.Count) total entries"
