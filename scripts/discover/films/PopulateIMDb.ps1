[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath,

    [Parameter(Mandatory)]
    [string]$ImdbDataPath,

    [switch]$UpdateImdbData
)

Import-Module "$PSScriptRoot\..\..\modules\tandoku-yaml.psm1"

# Preferred key order for film entries
$fieldOrder = @('wikidata', 'title', 'title-ja', 'type', 'originCountry', 'originalLanguage', 'year', 'imdb', 'myAnimeList', 'tmdb', 'providers')

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

# --- Download IMDb data if needed ---

if (-not (Test-Path $ImdbDataPath)) {
    New-Item -ItemType Directory -Path $ImdbDataPath | Out-Null
}

$ImdbDataPath = Resolve-Path $ImdbDataPath
$ratingsGzPath = Join-Path $ImdbDataPath 'title.ratings.tsv.gz'
$ratingsTsvPath = Join-Path $ImdbDataPath 'title.ratings.tsv'

if ($UpdateImdbData -or -not (Test-Path $ratingsTsvPath)) {
    $ratingsUrl = 'https://datasets.imdbws.com/title.ratings.tsv.gz'
    Write-Host "Downloading $ratingsUrl..."
    Invoke-WebRequest -Uri $ratingsUrl -OutFile $ratingsGzPath

    Write-Host "Extracting title.ratings.tsv..."
    $gzIn = [System.IO.File]::OpenRead($ratingsGzPath)
    $gzStream = [System.IO.Compression.GZipStream]::new($gzIn, [System.IO.Compression.CompressionMode]::Decompress)
    $tsvOut = [System.IO.File]::Create($ratingsTsvPath)
    $gzStream.CopyTo($tsvOut)
    $tsvOut.Close()
    $gzStream.Close()
    $gzIn.Close()
}

# --- Load IMDb ratings into lookup table ---

Write-Host "Loading IMDb ratings data..."
$ratings = @{}
$lineCount = 0
foreach ($line in [System.IO.File]::ReadLines($ratingsTsvPath)) {
    $lineCount++
    if ($lineCount -eq 1) { continue } # skip header
    $fields = $line.Split("`t")
    if ($fields.Count -ge 3) {
        $ratings[$fields[0]] = @{
            rating = [double]$fields[1]
            votes  = [int]$fields[2]
        }
    }
}

Write-Host "Loaded $($ratings.Count) IMDb ratings"

# --- Read films database ---

$films = [System.Collections.Generic.List[object]]::new()
foreach ($doc in @(Import-Yaml -LiteralPath $DatabasePath)) {
    $films.Add($doc)
}

Write-Host "Read $($films.Count) entries from films database"

# --- Populate IMDb ratings ---

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

    $data = $ratings[$imdbId]
    if (-not $data) {
        $notFound++
        continue
    }

    $film['imdb'] = [ordered]@{
        id     = $imdbId
        rating = $data.rating
        votes  = $data.votes
    }
    $updated++
}

Write-Host "Updated $updated, no IMDb ID $noImdbId, not found in data $notFound"

# Reorder keys and write films.yaml
for ($i = 0; $i -lt $films.Count; $i++) {
    $films[$i] = Reorder-FilmEntry $films[$i]
}
$films | Export-Yaml -Path $DatabasePath

Write-Host "Done - $($films.Count) total entries in $DatabasePath"
