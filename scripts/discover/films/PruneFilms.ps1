[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath
)

Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"
Import-Module "$PSScriptRoot/tandoku-discover-films.psm1"

# Removes stale film records that no longer have any origin. A record loses its
# origins when, for example, an IMDb-list-only film is pruned out of every list
# it belonged to (see ImportIMDbList.ps1); such a record no longer corresponds to
# any source and is removed here.

$films = Read-FilmsDatabase -LiteralPath $DatabasePath

Write-Host "Read $($films.Count) entries from films database"

$kept = [System.Collections.Generic.List[object]]::new()
$removed = 0
foreach ($film in $films) {
    if ($film.Contains('origin') -and $film['origin'] -and @($film['origin']).Count -gt 0) {
        $kept.Add($film)
    } else {
        $title = Get-DisplayTitle $film
        $imdbId = if ($film.Contains('imdb') -and $film['imdb']) { $film['imdb']['id'] } else { $null }
        $label = @($title, $imdbId | Where-Object { $_ }) -join ' '
        if (-not $label) { $label = '(no title)' }
        Write-Host "Removing record with no origin: $label"
        $removed++
    }
}

if ($removed -gt 0) {
    $kept | Export-Yaml -Path $DatabasePath
}

Write-Host "Done: $removed record(s) removed - $($kept.Count) total entries in $DatabasePath"
