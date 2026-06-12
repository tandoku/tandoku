[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath,

    [Parameter(Mandatory)]
    [string]$ImdbDataPath,

    [switch]$UpdateImdbData
)

Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"

# Title is a per-language dictionary (e.g. title.en, title.ja); prefer the English
# title for display, falling back to any available language.
function Get-DisplayTitle($film) {
    if (-not $film.title) { return $null }
    if ($film.title.en) { return $film.title.en }
    foreach ($value in $film.title.Values) {
        if ($value) { return $value }
    }
    return $null
}

# Preferred key order for film entries
$fieldOrder = @('wikidata', 'title', 'type', 'country', 'language', 'year', 'imdb', 'myAnimeList', 'tmdb', 'availability')

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

# --- Read films database ---

$films = [System.Collections.Generic.List[object]]::new()
foreach ($doc in @(Import-Yaml -LiteralPath $DatabasePath)) {
    $films.Add($doc)
}

Write-Host "Read $($films.Count) entries from films database"

# Collect the set of IMDb IDs referenced by the films database so we only load
# matching rows from the (very large) IMDb datasets.
$wantedIds = [System.Collections.Generic.HashSet[string]]::new()
foreach ($film in $films) {
    if ($film.imdb -and $film.imdb.id) {
        [void]$wantedIds.Add([string]$film.imdb.id)
    }
}

# --- Download IMDb data if needed ---

$imdbData = & "$PSScriptRoot/UpdateIMDbData.ps1" -ImdbDataPath $ImdbDataPath -Datasets 'title.basics', 'title.ratings', 'title.episode' -UpdateImdbData:$UpdateImdbData
$basicsTsvPath = $imdbData['title.basics']
$ratingsTsvPath = $imdbData['title.ratings']
$episodeTsvPath = $imdbData['title.episode']

# IMDb datasets use "\N" to represent missing values.
function Get-ImdbValue($value) {
    if ($value -eq '\N') { return $null }
    return $value
}

# --- Load IMDb title basics into lookup table ---

Write-Host "Loading IMDb title basics data..."
$basics = @{}
$lineCount = 0
foreach ($line in [System.IO.File]::ReadLines($basicsTsvPath)) {
    $lineCount++
    if ($lineCount -eq 1) { continue } # skip header
    $fields = $line.Split("`t")
    if ($fields.Count -lt 9) { continue }
    $id = $fields[0]
    if (-not $wantedIds.Contains($id)) { continue }

    $genresValue = Get-ImdbValue $fields[8]
    $genres = $null
    if ($genresValue) {
        $genres = [string[]]($genresValue.Split(','))
    }

    $basics[$id] = @{
        titleType     = Get-ImdbValue $fields[1]
        primaryTitle  = Get-ImdbValue $fields[2]
        originalTitle = Get-ImdbValue $fields[3]
        isAdult       = $fields[4] -eq '1'
        startYear     = Get-ImdbValue $fields[5]
        endYear       = Get-ImdbValue $fields[6]
        runtime       = Get-ImdbValue $fields[7]
        genres        = $genres
    }
}

Write-Host "Loaded $($basics.Count) IMDb title basics"

# --- Load IMDb ratings into lookup table ---

Write-Host "Loading IMDb ratings data..."
$ratings = @{}
$lineCount = 0
foreach ($line in [System.IO.File]::ReadLines($ratingsTsvPath)) {
    $lineCount++
    if ($lineCount -eq 1) { continue } # skip header
    $fields = $line.Split("`t")
    if ($fields.Count -ge 3) {
        $id = $fields[0]
        if (-not $wantedIds.Contains($id)) { continue }
        $ratings[$id] = @{
            rating = [double]$fields[1]
            votes  = [int]$fields[2]
        }
    }
}

Write-Host "Loaded $($ratings.Count) IMDb ratings"

# --- Load IMDb episode data and aggregate season/episode counts per series ---

Write-Host "Loading IMDb episode data..."
$episodeCounts = @{}
$seasonSets = @{}
$lineCount = 0
foreach ($line in [System.IO.File]::ReadLines($episodeTsvPath)) {
    $lineCount++
    if ($lineCount -eq 1) { continue } # skip header
    $fields = $line.Split("`t")
    if ($fields.Count -lt 4) { continue }
    $parentId = $fields[1]
    if (-not $wantedIds.Contains($parentId)) { continue }

    if (-not $episodeCounts.Contains($parentId)) {
        $episodeCounts[$parentId] = 0
        $seasonSets[$parentId] = [System.Collections.Generic.HashSet[string]]::new()
    }
    $episodeCounts[$parentId]++

    $seasonNumber = Get-ImdbValue $fields[2]
    if ($seasonNumber) {
        [void]$seasonSets[$parentId].Add($seasonNumber)
    }
}

Write-Host "Loaded episode data for $($episodeCounts.Count) series"

# --- Populate IMDb fields ---

$updated = 0
$notFound = 0
$noImdbId = 0
foreach ($film in $films) {
    $imdbId = $null
    if ($film.imdb) {
        $imdbId = $film.imdb.id
    }
    if (-not $imdbId) {
        $noImdbId++
        continue
    }

    $basic = $basics[$imdbId]
    $rating = $ratings[$imdbId]
    if (-not $basic -and -not $rating) {
        Write-Warning "IMDb ID '$imdbId' not found in IMDb data (title: $(Get-DisplayTitle $film), wikidata: $($film.wikidata))"
        $notFound++
        continue
    }

    $imdb = [ordered]@{ id = $imdbId }
    if ($basic) {
        if ($basic.primaryTitle) { $imdb['title'] = $basic.primaryTitle }
        if ($basic.originalTitle) { $imdb['originalTitle'] = $basic.originalTitle }
        if ($basic.titleType) { $imdb['type'] = $basic.titleType }
        if ($basic.isAdult) { $imdb['adult'] = $true }
        if ($basic.startYear) { $imdb['year'] = [int]$basic.startYear }
        if ($basic.endYear) { $imdb['endYear'] = [int]$basic.endYear }
        if ($basic.runtime) { $imdb['runtime'] = [int]$basic.runtime }
        if ($basic.genres) { $imdb['genres'] = $basic.genres }
    }

    $episodeCount = $episodeCounts[$imdbId]
    if ($episodeCount) {
        $imdb['seasons'] = $seasonSets[$imdbId].Count
        $imdb['episodes'] = $episodeCount
    }

    if ($rating) {
        $imdb['rating'] = $rating.rating
        $imdb['votes'] = $rating.votes
    }

    $film['imdb'] = $imdb
    $updated++
}

Write-Host "Updated $updated, no IMDb ID $noImdbId, not found in data $notFound"

# Reorder keys and write films.yaml
for ($i = 0; $i -lt $films.Count; $i++) {
    $films[$i] = Reorder-FilmEntry $films[$i]
}
$films | Export-Yaml -Path $DatabasePath

Write-Host "Done - $($films.Count) total entries in $DatabasePath"
