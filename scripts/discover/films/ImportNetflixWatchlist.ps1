[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Path,

    [Parameter(Mandatory)]
    [string]$DatabasePath
)

Import-Module "$PSScriptRoot\..\..\modules\tandoku-yaml.psm1"

# Read Netflix watchlist
$watchlist = Get-Content -Path $Path -Raw | ConvertFrom-Json

Write-Host "Read $($watchlist.Count) items from Netflix watchlist"

# Read existing films database
$films = [System.Collections.Generic.List[object]]::new()
if (Test-Path $DatabasePath) {
    foreach ($doc in @(Import-Yaml -LiteralPath $DatabasePath)) {
        $films.Add($doc)
    }
}

Write-Host "Read $($films.Count) existing entries from films database"

# Build lookup table for existing films by Netflix ID
$filmsByNetflixId = @{}
for ($i = 0; $i -lt $films.Count; $i++) {
    $film = $films[$i]
    if ($film.providers -and $film.providers.netflix -and $null -ne $film.providers.netflix.id) {
        $filmsByNetflixId[[string]$film.providers.netflix.id] = $i
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
        $film['providers']['netflix'] = [ordered]@{
            id        = [int]$videoId
            title     = $item.title
            watchlist = $true
        }
        $updated++
    } else {
        # Add new entry
        $newFilm = [ordered]@{
            providers = [ordered]@{
                netflix = [ordered]@{
                    id        = [int]$videoId
                    title     = $item.title
                    watchlist = $true
                }
            }
        }
        $films.Add($newFilm)
        $filmsByNetflixId[$videoId] = $films.Count - 1
        $added++
    }
}

# Mark removed watchlist items (in DB but no longer in imported watchlist)
$unmarked = 0
foreach ($film in $films) {
    if ($film.providers -and $film.providers.netflix -and $film.providers.netflix.watchlist -eq $true) {
        $netflixId = [string]$film.providers.netflix.id
        if (-not $watchlistNetflixIds.Contains($netflixId)) {
            $film.providers.netflix.watchlist = $false
            $unmarked++
        }
    }
}

# Write films.yaml
$films | Export-Yaml -Path $DatabasePath

Write-Host "Done: $added added, $updated updated, $unmarked unmarked - $($films.Count) total entries"
