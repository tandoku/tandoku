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
Import-Module "$PSScriptRoot/tandoku-discover-films.psm1"
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
    if ($film.availability -and $film.availability.netflix -and $null -ne $film.availability.netflix.id) {
        $filmsByNetflixId[[string]$film.availability.netflix.id] = $i
    }
}

# Track which Netflix IDs are in the current watchlist
$watchlistNetflixIds = [System.Collections.Generic.HashSet[string]]::new()
foreach ($item in $watchlist) {
    [void]$watchlistNetflixIds.Add($item.videoId)
}

# Process each Netflix watchlist item
$added = 0
$updated = 0
$skipped = 0
foreach ($item in $watchlist) {
    $videoId = $item.videoId

    if ($filmsByNetflixId.ContainsKey($videoId)) {
        # Update existing entry, preserving other netflix fields (e.g. type,
        # year, countryDetails added by ImportNetflixCatalog.ps1)
        $film = $films[$filmsByNetflixId[$videoId]]
        $netflix = $film['availability']['netflix']
        if ($item.title -and $Force) { $netflix['title'] = $item.title }
        $netflix['watchlist'] = $true
        Add-Origin $film 'netflix'
        $updated++
    } elseif ($Force) {
        # Add new entry
        $newFilm = [ordered]@{
            availability = [ordered]@{
                netflix = [ordered]@{
                    id        = [int]$videoId
                    title     = $item.title
                    watchlist = $true
                }
            }
        }
        Add-Origin $newFilm 'netflix'
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
    if ($film.availability -and $film.availability.netflix -and $film.availability.netflix.watchlist -eq $true) {
        $netflixId = [string]$film.availability.netflix.id
        if (-not $watchlistNetflixIds.Contains($netflixId)) {
            $film.availability.netflix.watchlist = $false
            $unmarked++
        }
    }
}

# Write films.yaml
$films | Export-Yaml -Path $DatabasePath

Write-Host "Done: $added added, $updated updated, $skipped skipped, $unmarked unmarked - $($films.Count) total entries"
