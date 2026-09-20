[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath,

    [Parameter(Mandatory)]
    [string]$TmdbDataPath,

    [string]$ApiKey = $env:TMDB_API_KEY,

    [switch]$UpdateTmdbData,

    [string]$LogPath
)

Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"
Import-Module "$PSScriptRoot/tandoku-discover-films.psm1"
Import-Module "$PSScriptRoot/../../modules/tandoku-log.psm1"

Initialize-TandokuLog -LogPath $LogPath
trap { Write-TandokuLogEntry 'ERROR' $_; break }

$tmdbApiBaseUrl = 'https://api.themoviedb.org/3'
$tmdbImageBaseUrl = 'https://image.tmdb.org/t/p'

function Save-TmdbCache($cache, [string]$cachePath, [bool]$numericKeys) {
    $sorted = [ordered]@{}
    $sortExpression = if ($numericKeys) { { [long]$_ } } else { { [string]$_ } }
    foreach ($key in ($cache.Keys | Sort-Object $sortExpression)) {
        $sorted[[string]$key] = $cache[$key]
    }
    $sorted | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $cachePath -Encoding UTF8
}

function Import-TmdbCache([string]$cachePath, [bool]$numericKeys) {
    $cache = [ordered]@{}
    if (-not (Test-Path -LiteralPath $cachePath)) {
        return $cache
    }

    $loaded = Get-Content -LiteralPath $cachePath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
    $sortExpression = if ($numericKeys) { { [long]$_ } } else { { [string]$_ } }
    foreach ($key in ($loaded.Keys | Sort-Object $sortExpression)) {
        $cache[[string]$key] = $loaded[$key]
    }
    return $cache
}

function Invoke-TmdbRequest(
    [string]$path,
    $cache,
    [string]$cacheKey,
    [string]$cachePath,
    [bool]$numericKeys
) {
    if (-not $UpdateTmdbData -and $cache.Contains($cacheKey)) {
        return $cache[$cacheKey]
    }

    if (-not $ApiKey) {
        throw 'A TMDB API key is required for uncached lookups. Pass -ApiKey or set the TMDB_API_KEY environment variable.'
    }

    $separator = if ($path.Contains('?')) { '&' } else { '?' }
    $uri = "$tmdbApiBaseUrl/$path${separator}api_key=$([uri]::EscapeDataString($ApiKey))"
    try {
        $response = Invoke-RestMethod -Uri $uri -Method Get
    }
    catch {
        if ([int]$_.Exception.Response.StatusCode -eq 404) {
            return $null
        }
        throw
    }
    $cache[$cacheKey] = $response
    Save-TmdbCache $cache $cachePath $numericKeys
    return $response
}

function Get-TmdbKindFromImdbType([string]$imdbType) {
    if ($imdbType -in @('movie', 'short', 'tvMovie', 'video')) {
        return 'movie'
    }
    if ($imdbType -in @('tvSeries', 'tvMiniSeries')) {
        return 'tv-series'
    }
    return $null
}

function Find-TmdbByImdbId($film, [string]$imdbId) {
    $response = Invoke-TmdbRequest `
        "find/$([uri]::EscapeDataString($imdbId))?external_source=imdb_id" `
        $script:TmdbImdbCache `
        $imdbId `
        $script:TmdbImdbCachePath `
        $false
    if (-not $response) {
        Write-Warning "TMDB lookup failed for IMDb ID '$imdbId' (title: $(Get-DisplayTitle $film), wikidata: $($film.wikidata))"
        return $null
    }
    $matches = [System.Collections.Generic.List[object]]::new()

    foreach ($result in @($response.movie_results)) {
        $matches.Add([pscustomobject]@{ kind = 'movie'; result = $result })
    }
    foreach ($result in @($response.tv_results)) {
        $matches.Add([pscustomobject]@{ kind = 'tv-series'; result = $result })
    }

    $preferredKind = Get-TmdbKindFromImdbType $film.imdb.type
    if ($preferredKind) {
        $preferredMatches = @($matches | Where-Object { $_.kind -eq $preferredKind })
        if ($preferredMatches.Count -gt 0) {
            $matches = $preferredMatches
        }
    }

    if ($matches.Count -eq 0) {
        Write-Warning "No TMDB match for IMDb ID '$imdbId' (title: $(Get-DisplayTitle $film), wikidata: $($film.wikidata))"
        return $null
    }
    if ($matches.Count -gt 1) {
        $matchDescription = @($matches | ForEach-Object { "$($_.kind):$($_.result.id)" }) -join ', '
        Write-Warning "IMDb ID '$imdbId' has multiple TMDB matches [$matchDescription] (title: $(Get-DisplayTitle $film), wikidata: $($film.wikidata)); skipping"
        return $null
    }

    $match = $matches[0]
    return Get-TmdbById $film ([int]$match.result.id) ([string]$match.kind)
}

function Get-TmdbById($film, [int]$tmdbId, [string]$tmdbKind) {
    $tmdbPath = switch ($tmdbKind) {
        'movie' { 'movie' }
        'tv-series' { 'tv' }
        default {
            Write-Warning "Unsupported tmdb.kind '$tmdbKind' for TMDB ID '$tmdbId' (title: $(Get-DisplayTitle $film), wikidata: $($film.wikidata))"
            return $null
        }
    }

    $cache = if ($tmdbKind -eq 'movie') { $script:TmdbMovieCache } else { $script:TmdbTvSeriesCache }
    $cachePath = if ($tmdbKind -eq 'movie') { $script:TmdbMovieCachePath } else { $script:TmdbTvSeriesCachePath }
    $response = Invoke-TmdbRequest "$tmdbPath/$tmdbId" $cache ([string]$tmdbId) $cachePath $true
    if (-not $response) {
        Write-Warning "TMDB $tmdbKind ID '$tmdbId' was not found (title: $(Get-DisplayTitle $film), wikidata: $($film.wikidata))"
        return $null
    }
    return [pscustomobject]@{ kind = $tmdbKind; result = $response }
}

function Set-TmdbData($film, $match) {
    $tmdbId = [int]$match.result.id
    $tmdb = [ordered]@{
        id   = $tmdbId
        kind = $match.kind
    }

    if ($match.result.poster_path) {
        $posterPath = [string]$match.result.poster_path
        if (-not $posterPath.StartsWith('/')) {
            $posterPath = "/$posterPath"
        }
        $tmdb['images'] = [ordered]@{
            small = "$tmdbImageBaseUrl/w342$posterPath"
            large = "$tmdbImageBaseUrl/w780$posterPath"
        }
    } else {
        Write-Warning "TMDB $($match.kind) ID '$tmdbId' has no poster image (title: $(Get-DisplayTitle $film), wikidata: $($film.wikidata))"
    }

    if ($film.tmdb -is [System.Collections.IDictionary]) {
        foreach ($key in @($film.tmdb.Keys)) {
            if ($key -ne 'images' -and -not $tmdb.Contains($key)) {
                $tmdb[$key] = $film.tmdb[$key]
            }
        }
    }

    $film['tmdb'] = $tmdb
}

$films = Read-FilmsDatabase -LiteralPath $DatabasePath
Write-Host "Read $($films.Count) entries from films database"

if (-not (Test-Path -LiteralPath $TmdbDataPath)) {
    New-Item -ItemType Directory -Path $TmdbDataPath | Out-Null
}
$TmdbDataPath = (Resolve-Path -LiteralPath $TmdbDataPath).Path

$script:TmdbImdbCachePath = Join-Path $TmdbDataPath 'tmdb-imdb.json'
$script:TmdbMovieCachePath = Join-Path $TmdbDataPath 'tmdb-movies.json'
$script:TmdbTvSeriesCachePath = Join-Path $TmdbDataPath 'tmdb-tv-series.json'
$script:TmdbImdbCache = Import-TmdbCache $script:TmdbImdbCachePath $false
$script:TmdbMovieCache = Import-TmdbCache $script:TmdbMovieCachePath $true
$script:TmdbTvSeriesCache = Import-TmdbCache $script:TmdbTvSeriesCachePath $true

$matched = 0
$notFound = 0
$missingIdentifier = 0

foreach ($film in $films) {
    $match = $null
    if ($film.tmdb -and $film.tmdb.id -and $film.tmdb.kind) {
        $match = Get-TmdbById $film ([int]$film.tmdb.id) ([string]$film.tmdb.kind)
        if (-not $match -and $film.imdb -and $film.imdb.id) {
            $imdbId = [string]$film.imdb.id
            if ($imdbId -match '^tt\d+$') {
                Write-Warning "Falling back to IMDb ID '$imdbId' for '$(Get-DisplayTitle $film)'"
                $match = Find-TmdbByImdbId $film $imdbId
            } else {
                Write-Warning "Invalid IMDb ID '$imdbId' for TMDB fallback (title: $(Get-DisplayTitle $film), wikidata: $($film.wikidata))"
            }
        }
    } elseif ($film.imdb -and $film.imdb.id) {
        $imdbId = [string]$film.imdb.id
        if ($imdbId -notmatch '^tt\d+$') {
            Write-Warning "Invalid IMDb ID '$imdbId' for TMDB lookup (title: $(Get-DisplayTitle $film), wikidata: $($film.wikidata))"
            $notFound++
            continue
        }
        $match = Find-TmdbByImdbId $film $imdbId
    } else {
        Write-Warning "No complete tmdb.id/kind or imdb.id for '$(Get-DisplayTitle $film)' (wikidata: $($film.wikidata))"
        $missingIdentifier++
        continue
    }

    if (-not $match) {
        $notFound++
        continue
    }

    Set-TmdbData $film $match
    $matched++
}

for ($i = 0; $i -lt $films.Count; $i++) {
    $films[$i] = Format-FilmEntry $films[$i]
}
$films | Export-Yaml -Path $DatabasePath

Write-Host "Done: $matched matched, $notFound not found, $missingIdentifier missing identifiers - $($films.Count) total entries"
