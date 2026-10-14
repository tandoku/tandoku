[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Path,

    [Parameter(Mandatory)]
    [string]$DatabasePath,

    [string]$LogPath,

    [switch]$Force
)

Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"
Import-Module "$PSScriptRoot/tandoku-catalog-films.psm1"
Import-Module "$PSScriptRoot/../../modules/tandoku-log.psm1"

# When -LogPath is supplied, additionally record warnings and errors (including
# uncaught terminating errors) to that file. See tandoku-log.psm1.
Initialize-TandokuLog -LogPath $LogPath
trap { Write-TandokuLogEntry 'ERROR' $_; break }

# Read Netflix watchlist
$watchlist = Get-Content -Path $Path -Raw | ConvertFrom-Json

Write-Host "Read $($watchlist.Count) items from Netflix watchlist"

# Read existing films database
$films = Read-FilmsDatabase -LiteralPath $DatabasePath -AllowMissing

Write-Host "Read $($films.Count) existing entries from films database"

# Build lookup table for existing films by Netflix ID
$filmsByNetflixId = @{}
for ($i = 0; $i -lt $films.Count; $i++) {
    $film = $films[$i]
    $netflixId = Get-FilmIdentifier $film 'netflix'
    if ($null -ne $netflixId) {
        $filmsByNetflixId[[string]$netflixId] = $i
    }
}

# Track which Netflix IDs are in the current watchlist
$watchlistNetflixIds = [System.Collections.Generic.HashSet[string]]::new()
foreach ($item in $watchlist) {
    [void]$watchlistNetflixIds.Add([string]$item.videoId)
}

# Process each Netflix watchlist item
$added = 0
$updated = 0
$skipped = 0
foreach ($item in $watchlist) {
    $videoId = [string]$item.videoId

    if ($filmsByNetflixId.ContainsKey($videoId)) {
        $film = $films[$filmsByNetflixId[$videoId]]
        $netflix = Get-FilmSource $film 'netflix-watchlist' -Create
        $netflix['title'] = $item.title
        $netflix['watchlist'] = $true
        Add-Origin $film 'netflix-watchlist'
        $updated++
    } elseif ($Force) {
        # Add new entry
        $newFilm = [ordered]@{}
        Set-FilmIdentifier $newFilm 'netflix' ([int]$videoId)
        $netflix = Get-FilmSource $newFilm 'netflix-watchlist' -Create
        $netflix['title'] = $item.title
        $netflix['watchlist'] = $true
        Add-Origin $newFilm 'netflix-watchlist'
        $films.Add($newFilm)
        $filmsByNetflixId[$videoId] = $films.Count - 1
        $added++
    } else {
        Write-Warning "Skipping $($item.title) ($videoId) not present in database and -Force not specified"
        $skipped++
    }
}

# Mark removed watchlist items (in DB but no longer in imported watchlist)
$unmarked = 0
foreach ($film in $films) {
    $netflix = Get-FilmSource $film 'netflix-watchlist'
    if ($netflix -and $netflix.watchlist -eq $true) {
        $netflixId = [string](Get-FilmIdentifier $film 'netflix')
        if (-not $watchlistNetflixIds.Contains($netflixId)) {
            $netflix['watchlist'] = $false
            $unmarked++
        }
    }
}

# Write film-catalog.yaml
for ($i = 0; $i -lt $films.Count; $i++) {
    $films[$i] = Format-FilmEntry $films[$i]
}
$films | Export-Yaml -Path $DatabasePath

Write-Host "Done: $added added, $updated updated, $skipped skipped, $unmarked unmarked - $($films.Count) total entries"


