[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$IMDbExportsPath,

    [Parameter(Mandatory)]
    [string]$CsvPath,

    [string]$LogPath
)

Import-Module "$PSScriptRoot/../../modules/tandoku-utils.psm1"
Import-Module "$PSScriptRoot/../../modules/tandoku-log.psm1"

# When -LogPath is supplied, additionally record warnings and errors (including
# uncaught terminating errors) to that file. See tandoku-log.psm1.
Initialize-TandokuLog -LogPath $LogPath
trap { Write-TandokuLogEntry 'ERROR' $_; break }

# Parses a saved IMDb exports page (https://www.imdb.com/exports/) and downloads
# every ready title-list export as a CSV file under $CsvPath. Each list is saved
# as <kebab-cased list name>.csv so the file name doubles as the list name when
# imported by ImportIMDbList.ps1.
#
# NOTE: the presigned CSV URLs embedded in the page expire a few minutes after
# the page is loaded, so save the exports page and run this script promptly
# (re-save the page if the downloads fail with an expired/forbidden error).

# Converts a list name to a file-name-friendly kebab-case slug (lower-case ASCII
# words separated by single hyphens).
function ConvertTo-KebabCase([string]$text) {
    $slug = $text.ToLowerInvariant() -replace '[^a-z0-9]+', '-'
    return $slug.Trim('-')
}

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
    Write-Warning 'No ready title-list exports to download.'
    return
}

# --- Download each list as <kebab-name>.csv ---

$csvDir = ConvertPath $CsvPath
if (-not (Test-Path -LiteralPath $csvDir)) {
    New-Item -ItemType Directory -Path $csvDir | Out-Null
}

# Assign each export its slug, then sort by slug (with list id as a stable
# tiebreak) so collision suffixes are assigned deterministically regardless of
# the order the exports appear on the page.
foreach ($export in $exports) {
    $slug = ConvertTo-KebabCase $export.ListName
    if (-not $slug) { $slug = $export.ListId }
    $export | Add-Member -NotePropertyName Slug -NotePropertyValue $slug
}
$exports = @($exports | Sort-Object Slug, ListId)

$usedNames = @{}
$downloaded = 0
foreach ($export in $exports) {
    $slug = $export.Slug

    # Disambiguate distinct lists that slug to the same file name.
    $name = $slug
    if ($usedNames.ContainsKey($slug)) {
        $usedNames[$slug]++
        $name = "$slug-$($usedNames[$slug])"
        Write-Warning "List name '$($export.ListName)' collides with another list as '$slug.csv'; saving as '$name.csv' instead."
    } else {
        $usedNames[$slug] = 1
    }

    $destination = Join-Path $csvDir "$name.csv"
    Write-Host "Downloading list '$($export.ListName)' ($($export.ListId)) -> $name.csv"
    try {
        Invoke-WebRequest -Uri $export.ResultUrl -OutFile $destination
    } catch {
        throw "Failed to download CSV for list '$($export.ListName)' ($($export.ListId)): $($_.Exception.Message). The presigned download URL may have expired - re-save the IMDb exports page and try again."
    }
    $downloaded++
}

Write-Host "Done: downloaded $downloaded list(s) to $csvDir"
