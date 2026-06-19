[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Path,

    [Parameter(Mandatory)]
    [string]$DatabasePath
)

Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"
Import-Module "$PSScriptRoot/tandoku-discover-films.psm1"

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
foreach ($item in $watchlist) {
    $videoId = $item.videoId

    if ($filmsByNetflixId.ContainsKey($videoId)) {
        # Update existing entry
        $film = $films[$filmsByNetflixId[$videoId]]
        $film['availability']['netflix'] = [ordered]@{
            id        = [int]$videoId
            title     = $item.title
            watchlist = $true
        }
        Add-Origin $film 'netflix'
        $updated++
    } else {
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

Write-Host "Done: $added added, $updated updated, $unmarked unmarked - $($films.Count) total entries"
