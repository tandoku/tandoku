[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath,

    [Parameter(Mandatory)]
    [string]$NativelyDataPath,

    [switch]$UpdateNativelyData,

    [string]$NativelyLanguage = 'ja',

    [string[]]$OriginalLanguage = @()
)

Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"

# Ensure UTF-8 encoding for yq compatibility
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8

# Natively's API and URLs use three-letter ISO 639-2 language codes; convert the
# two-letter ISO 639-1 code supplied via -NativelyLanguage.
$nativelyLanguageCode = [System.Globalization.CultureInfo]::GetCultureInfo($NativelyLanguage).ThreeLetterISOLanguageName

# Preferred key order for film entries
$fieldOrder = @('wikidata', 'title', 'title-ja', 'type', 'originCountry', 'originalLanguage', 'year', 'imdb', 'myAnimeList', 'tmdb', 'natively', 'providers')

$nativelyBaseUrl = 'https://learnnatively.com'
$nativelyHeaders = @{
    "User-Agent" = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    "Accept"     = "*/*"
    "Referer"    = "https://learnnatively.com/search/$nativelyLanguageCode/videos/"
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

function Format-NativelyLevel($rating) {
    if (-not $rating -or $null -eq $rating.lvl) { return $null }
    return [int]$rating.lvl
}

# Normalize a single Natively search result into a record keyed by TMDB id + kind.
# Movies map to a 'movie' record; TV series (and TV seasons, via their parent
# series) map to a 'tv-series' record so they can be matched against the films
# database, where tmdb.kind is either 'movie' or 'tv-series'.
function ConvertTo-NativelyRecord($result) {
    if ($result.widget -eq 'series') {
        $series = $result.series
        if ($null -eq $series.tmdb_id) { return $null }
        return [ordered]@{
            tmdbId    = [int]$series.tmdb_id
            tmdbKind  = 'tv-series'
            title     = $series.title
            url       = "$nativelyBaseUrl$($series.url)"
            level     = Format-NativelyLevel $series.rating
            temporary = [bool]$series.rating.temporary
            source    = 'series'
        }
    }

    $item = $result.item
    if ($item.media_type -eq 'movie') {
        if ($null -eq $item.tmdb_id) { return $null }
        return [ordered]@{
            tmdbId    = [int]$item.tmdb_id
            tmdbKind  = 'movie'
            title     = $item.title
            url       = "$nativelyBaseUrl$($item.url)"
            level     = Format-NativelyLevel $item.rating
            temporary = [bool]$item.rating.temporary
            source    = 'movie'
        }
    }

    # Other item kinds (e.g. tv_season) belong to a parent series; key them by
    # the parent series' TMDB id so a 'tv-series' entry can still match when no
    # standalone series widget is present.
    if ($item.series -and $null -ne $item.series.tmdb_id) {
        return [ordered]@{
            tmdbId    = [int]$item.series.tmdb_id
            tmdbKind  = 'tv-series'
            title     = $item.series.title
            url       = "$nativelyBaseUrl$($item.series.url)"
            level     = Format-NativelyLevel $item.rating
            temporary = [bool]$item.rating.temporary
            source    = 'season'
        }
    }

    return $null
}

# Fetch Natively videos for $language matching the given item type / series
# grouping, paging until all pages have been retrieved.
function Get-NativelyVideos($language, $itype, $seriesMode, $label) {
    $videos = [System.Collections.Generic.List[object]]::new()
    $page = 1
    $numPages = $null
    $totalCount = 0

    while ($true) {
        $url = "$nativelyBaseUrl/api/ninja/search/videos/?language=$language&itype=$itype&series=$seriesMode&p=$page"
        $resp = Invoke-RestMethod -Uri $url -Headers $nativelyHeaders

        if ($null -eq $numPages) {
            $numPages = [int]$resp.num_of_pages
            $totalCount = [int]$resp.total_count
            Write-Host "Fetching $totalCount Natively $label across $numPages page(s)..."
        }

        foreach ($result in @($resp.results)) {
            $record = ConvertTo-NativelyRecord $result
            if ($record) {
                $videos.Add($record)
            }
        }

        Write-Host "  $label page $page/$numPages ($($videos.Count) records so far)"

        if ($page -ge $numPages) { break }
        $page++

        # Throttle requests to avoid overwhelming the site
        Start-Sleep -Milliseconds (1000 + (Get-Random -Maximum 1000))
    }

    return [ordered]@{
        language   = $language
        itype      = $itype
        series     = $seriesMode
        fetchedAt  = (Get-Date).ToString('o')
        totalCount = $totalCount
        videos     = $videos
    }
}

# Fetch (or load cached) Natively records for one slice (movies or TV) and
# persist them to their own JSON file.
function Get-OrFetchNatively($jsonPath, $itype, $seriesMode, $label) {
    if ($UpdateNativelyData -or -not (Test-Path $jsonPath)) {
        $data = Get-NativelyVideos $nativelyLanguageCode $itype $seriesMode $label
        $data | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
        Write-Host "Saved $($data.videos.Count) Natively $label records to $jsonPath"
    }
    else {
        $data = Get-Content -LiteralPath $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
        Write-Host "Loaded $($data.videos.Count) Natively $label records from $jsonPath"
    }
    return $data
}

# Build a lookup table keyed by "<tmdbKind>|<tmdbId>". When the same key appears
# more than once, prefer a record that has a usable level, then prefer series
# widgets and movies over season-derived records.
function Get-NativelyRecordPriority($video) {
    $hasLevel = if ($null -ne $video.level) { 2 } else { 0 }
    $sourceRank = if ($video.source -eq 'season') { 0 } else { 1 }
    return $hasLevel + $sourceRank
}

function Build-NativelyIndex($videos) {
    $index = @{}
    foreach ($video in $videos) {
        if ($null -eq $video.tmdbId) { continue }
        $key = "$($video.tmdbKind)|$([int]$video.tmdbId)"
        $priority = Get-NativelyRecordPriority $video
        $existing = $index[$key]
        if ($null -eq $existing -or $priority -gt $existing.priority) {
            $index[$key] = [pscustomobject]@{ video = $video; priority = $priority }
        }
    }
    return $index
}

# --- Read films database ---

$films = [System.Collections.Generic.List[object]]::new()
foreach ($doc in @(Import-Yaml -LiteralPath $DatabasePath)) {
    $films.Add($doc)
}

Write-Host "Read $($films.Count) entries from films database"

# Filter to entries eligible for Natively data. Matching is done by TMDB id/kind,
# so any entry with tmdb data is eligible. When -OriginalLanguage is supplied,
# only entries whose originalLanguage includes at least one of those two-letter
# codes are processed. Every eligible entry is (re)looked up on each run so
# existing Natively metadata is refreshed against the current dataset, not just
# entries that are missing it.
$needsNatively = @()
foreach ($film in $films) {
    if (-not $film.tmdb) {
        continue
    }
    if ($OriginalLanguage.Count -gt 0) {
        $filmLanguages = @($film.originalLanguage)
        $hasLanguageMatch = $false
        foreach ($language in $filmLanguages) {
            if ($OriginalLanguage -contains $language) {
                $hasLanguageMatch = $true
                break
            }
        }
        if (-not $hasLanguageMatch) {
            continue
        }
    }
    $needsNatively += $film
}

# Skip fetching the (large) Natively dataset entirely when there is nothing to
# match and the caller has not explicitly asked to refresh the local data.
if ($needsNatively.Count -eq 0 -and -not $UpdateNativelyData) {
    Write-Host "No entries need Natively lookup"
    return
}

# --- Download Natively data if needed ---

if (-not (Test-Path $NativelyDataPath)) {
    New-Item -ItemType Directory -Path $NativelyDataPath | Out-Null
}

$NativelyDataPath = Resolve-Path $NativelyDataPath
$moviesJsonPath = Join-Path $NativelyDataPath "natively-movies-$nativelyLanguageCode.json"
$tvJsonPath = Join-Path $NativelyDataPath "natively-tv-$nativelyLanguageCode.json"

# Movies: itype=movie with series=all_volumes expands collections / movie-series
# into their individual movie volumes so each movie is keyed by its own TMDB id.
$moviesData = Get-OrFetchNatively $moviesJsonPath 'movie' 'all_volumes' 'movies'

# TV: itype=tv_season with series=series groups seasons under their parent series
# so entries match against the series' TMDB id.
$tvData = Get-OrFetchNatively $tvJsonPath 'tv_season' 'series' 'TV series'

if ($needsNatively.Count -eq 0) {
    Write-Host "No entries need Natively lookup"
    return
}

$allVideos = @()
$allVideos += @($moviesData.videos)
$allVideos += @($tvData.videos)
$index = Build-NativelyIndex $allVideos

Write-Host "$($needsNatively.Count) entries need Natively lookup"

$matched = 0
$notFound = 0

foreach ($film in $needsNatively) {
    $title = $film.title
    $tmdbId = [int]$film.tmdb.id
    $tmdbKind = $film.tmdb.kind

    $key = "$tmdbKind|$tmdbId"
    $entry = $index[$key]
    $matchedResult = if ($entry) { $entry.video } else { $null }

    if ($matchedResult -and $null -ne $matchedResult.level) {
        $natively = [ordered]@{
            language = $NativelyLanguage
            level    = [int]$matchedResult.level
        }
        if ($matchedResult.temporary) {
            $natively['temporaryLevel'] = $true
        }
        $natively['url'] = $matchedResult.url
        $film['natively'] = $natively
        $matched++
        Write-Host "Matched '$title' -> $($matchedResult.url) (level $($matchedResult.level))"
    } else {
        $tmdbPath = if ($tmdbKind -eq 'movie') { 'movie' } else { 'tv' }
        $tmdbUrl = "https://www.themoviedb.org/$tmdbPath/$tmdbId"
        $nativelySearchUrl = "$nativelyBaseUrl/search/$nativelyLanguageCode/videos/?q=$([uri]::EscapeDataString([string]$title))"
        Write-Warning "No Natively match for '$title' (tmdb $tmdbUrl natively $nativelySearchUrl wikidata=$($film.wikidata))"
        $notFound++
    }
}

# Reorder keys and write films.yaml
for ($i = 0; $i -lt $films.Count; $i++) {
    $films[$i] = Reorder-FilmEntry $films[$i]
}
$films | Export-Yaml -Path $DatabasePath

Write-Host "Done: $matched matched, $notFound not found - $($films.Count) total entries"
