[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath,

    [Parameter(Mandatory)]
    [string[]]$CsvPath
)

Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"
Import-Module "$PSScriptRoot/tandoku-discover-films.psm1"

# Imports one or more IMDb list CSV exports into the films database. Each CSV's
# file name (without the .csv extension) is used as the list name recorded under
# imdb.lists.<list-name>:<rank>. Entries are matched by imdb.id (created if
# absent) and repeated runs merge without smashing other lists or fields.
#
# -CsvPath accepts any mix of: a directory (all *.csv files within it), a CSV
# file path, or a wildcard pattern (e.g. lists-*.csv).

# --- Resolve the set of CSV files to import ---

$csvFiles = [System.Collections.Generic.List[string]]::new()
$seenFiles = [System.Collections.Generic.HashSet[string]]::new()
foreach ($entry in $CsvPath) {
    $resolved = if (Test-Path -LiteralPath $entry -PathType Container) {
        # Directory: import every *.csv file within it.
        Get-ChildItem -Path (Join-Path $entry '*.csv') -File
    } else {
        # File path or wildcard pattern.
        Get-ChildItem -Path $entry -File -ErrorAction SilentlyContinue
    }

    $resolved = @($resolved | Where-Object { $_.Extension -eq '.csv' } | Sort-Object FullName)
    if ($resolved.Count -eq 0) {
        Write-Warning "No CSV files matched '$entry'."
        continue
    }
    foreach ($file in $resolved) {
        if ($seenFiles.Add($file.FullName)) {
            $csvFiles.Add($file.FullName)
        }
    }
}

if ($csvFiles.Count -eq 0) {
    throw 'No CSV files to import.'
}

Write-Host "Importing $($csvFiles.Count) list CSV file(s)"

# --- Read existing films database ---

$films = Read-FilmsDatabase -LiteralPath $DatabasePath -AllowMissing

Write-Host "Read $($films.Count) existing entries from films database"

# Build lookup table for existing films by IMDb ID
$filmsByImdbId = @{}
for ($i = 0; $i -lt $films.Count; $i++) {
    $film = $films[$i]
    if ($film.imdb -and $film.imdb.id) {
        $filmsByImdbId[[string]$film.imdb.id] = $i
    }
}

# Merges a single list's CSV rows into the database. Matches existing entries by
# imdb.id (creating new ones if absent) and records the title's rank under
# imdb.lists.<list-name> without disturbing other lists or fields. Acts as the
# authoritative source for the list it imports: titles previously recorded under
# this list name but absent from the CSV have their imdb.lists.<list-name> entry
# removed (and imdb.lists dropped entirely once empty).
function Import-ListRows($listName, $rows) {
    $added = 0
    $updated = 0
    $removed = 0
    $idsInList = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($row in $rows) {
        $id = $row.Const
        if (-not $id) { continue }
        [void]$idsInList.Add([string]$id)
        $title = $row.Title
        $rank = if ($row.Position) { [int]$row.Position } else { $null }

        if ($filmsByImdbId.ContainsKey($id)) {
            $film = $films[$filmsByImdbId[$id]]
            $imdb = $film['imdb']
            if ($title) { $imdb['title'] = $title }
            $updated++
        } else {
            $imdb = [ordered]@{
                id    = $id
                title = $title
            }
            $film = [ordered]@{ imdb = $imdb }
            $films.Add($film)
            $filmsByImdbId[$id] = $films.Count - 1
            $added++
        }

        if (-not $imdb.Contains('lists') -or -not $imdb['lists']) {
            $imdb['lists'] = [ordered]@{}
        }
        $imdb['lists'][$listName] = $rank

        Add-Origin $film 'imdb'
    }

    # Prune stale membership: any film still carrying this list whose IMDb ID is
    # no longer in the CSV is removed from the list, dropping imdb.lists when it
    # becomes empty.
    foreach ($film in $films) {
        if (-not ($film.Contains('imdb') -and $film['imdb'])) { continue }
        $imdb = $film['imdb']
        if (-not ($imdb.Contains('lists') -and $imdb['lists'] -and $imdb['lists'].Contains($listName))) { continue }
        if (-not $idsInList.Contains([string]$imdb['id'])) {
            $imdb['lists'].Remove($listName)
            if ($imdb['lists'].Count -eq 0) {
                $imdb.Remove('lists')
                # The film is no longer a member of any imdb list, so the imdb
                # origin tag no longer applies (the imdb id/title cross-reference
                # fields are kept).
                Remove-Origin $film 'imdb'
            }
            $removed++
        }
    }

    return [pscustomobject]@{ Added = $added; Updated = $updated; Removed = $removed }
}

# --- Import each CSV ---

# Warn when two distinct CSV files share a base name, since they map to the same
# imdb.lists entry and the later import would overwrite the earlier one's ranks.
$listNameSources = @{}
foreach ($file in $csvFiles) {
    $listName = [System.IO.Path]::GetFileNameWithoutExtension($file)
    if (-not $listNameSources.ContainsKey($listName)) {
        $listNameSources[$listName] = [System.Collections.Generic.List[string]]::new()
    }
    $listNameSources[$listName].Add($file)
}
foreach ($listName in $listNameSources.Keys) {
    $sources = $listNameSources[$listName]
    if ($sources.Count -gt 1) {
        Write-Warning "Multiple CSV files map to list '$listName' (later imports overwrite earlier ranks): $($sources -join ', ')"
    }
}

$totalAdded = 0
$totalUpdated = 0
$totalRemoved = 0
foreach ($file in $csvFiles) {
    $listName = [System.IO.Path]::GetFileNameWithoutExtension($file)
    Write-Host "Importing list '$listName' from $(Split-Path -Leaf $file)..."

    $rows = Import-Csv -LiteralPath $file
    $result = Import-ListRows $listName $rows
    Write-Host "  $($rows.Count) title(s): $($result.Added) added, $($result.Updated) updated, $($result.Removed) removed"
    $totalAdded += $result.Added
    $totalUpdated += $result.Updated
    $totalRemoved += $result.Removed
}

# --- Reorder keys and write films.yaml ---

for ($i = 0; $i -lt $films.Count; $i++) {
    $films[$i] = Format-FilmEntry $films[$i]
}
$films | Export-Yaml -Path $DatabasePath

Write-Host "Done: $totalAdded added, $totalUpdated updated, $totalRemoved removed across $($csvFiles.Count) list(s) - $($films.Count) total entries in $DatabasePath"
