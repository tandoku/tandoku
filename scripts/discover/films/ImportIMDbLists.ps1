[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath,

    [Parameter(Mandatory)]
    [string]$IMDbExportsPath
)

Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"
Import-Module "$PSScriptRoot/tandoku-discover-films.psm1"

# Parses the saved IMDb exports page (https://www.imdb.com/exports/) and imports
# every ready list export into the films database. The page embeds the export
# job metadata (list name/id) and a presigned S3 CSV download URL for each export
# in its Next.js `__NEXT_DATA__` JSON blob.
#
# NOTE: the presigned CSV URLs expire a few minutes after the page is loaded, so
# save the exports page and run this script promptly (re-save the page if the
# downloads fail with an expired/forbidden error).

# --- Extract export jobs from the saved exports page ---

$html = Get-Content -LiteralPath $IMDbExportsPath -Raw

$match = [regex]::Match(
    $html,
    '<script id="__NEXT_DATA__" type="application/json">(.*?)</script>',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $match.Success) {
    throw "Could not find __NEXT_DATA__ in '$IMDbExportsPath'. Is this a saved IMDb exports page (https://www.imdb.com/exports/)?"
}

$nextData = $match.Groups[1].Value | ConvertFrom-Json -AsHashtable

$edges = $nextData.props.pageProps.mainColumnData.getExports.edges
if (-not $edges) {
    throw 'No export jobs found in the exports page (the getExports edges were empty).'
}

# Keep the most recent ready title-list export per list id (the edges are ordered
# newest-first, so the first occurrence of each list id wins).
$exports = [System.Collections.Generic.List[object]]::new()
$seenListIds = [System.Collections.Generic.HashSet[string]]::new()
foreach ($edge in $edges) {
    $node = $edge.node
    if ($node.exportType -ne 'LIST') { continue }
    if ($node.status -and $node.status.id -ne 'READY') { continue }

    $meta = $node.listExportMetadata
    if (-not $meta -or $meta.listType -ne 'TITLES') { continue }
    if (-not $node.resultUrl) { continue }

    if (-not $seenListIds.Add([string]$meta.id)) { continue }

    $exports.Add([pscustomobject]@{
        ListId    = [string]$meta.id
        ListName  = [string]$meta.name
        ResultUrl = [string]$node.resultUrl
    })
}

Write-Host "Found $($exports.Count) ready title-list export(s) in $IMDbExportsPath"

if ($exports.Count -eq 0) {
    Write-Warning 'No ready title-list exports to import.'
    return
}

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
# imdb.lists.<list-name> without disturbing other lists or fields.
function Import-ListRows($listName, $rows) {
    $added = 0
    $updated = 0
    foreach ($row in $rows) {
        $id = $row.Const
        if (-not $id) { continue }
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
    return [pscustomobject]@{ Added = $added; Updated = $updated }
}

# --- Download and import each list ---

$totalAdded = 0
$totalUpdated = 0
foreach ($export in $exports) {
    Write-Host "Importing list '$($export.ListName)' ($($export.ListId))..."

    $tempCsv = [System.IO.Path]::GetTempFileName()
    try {
        try {
            Invoke-WebRequest -Uri $export.ResultUrl -OutFile $tempCsv
        } catch {
            throw "Failed to download CSV for list '$($export.ListName)' ($($export.ListId)): $($_.Exception.Message). The presigned download URL may have expired - re-save the IMDb exports page and try again."
        }

        $rows = Import-Csv -LiteralPath $tempCsv
        $result = Import-ListRows $export.ListName $rows
        Write-Host "  $($rows.Count) title(s): $($result.Added) added, $($result.Updated) updated"
        $totalAdded += $result.Added
        $totalUpdated += $result.Updated
    } finally {
        Remove-Item -LiteralPath $tempCsv -ErrorAction SilentlyContinue
    }
}

# --- Reorder keys and write films.yaml ---

for ($i = 0; $i -lt $films.Count; $i++) {
    $films[$i] = Format-FilmEntry $films[$i]
}
$films | Export-Yaml -Path $DatabasePath

Write-Host "Done: $totalAdded added, $totalUpdated updated across $($exports.Count) list(s) - $($films.Count) total entries in $DatabasePath"
