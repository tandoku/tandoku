[CmdletBinding()]
param(
    [string]$Path = '.',
    [switch]$Force,
    [switch]$NetflixCatalog,
    [int]$NetflixRequestLimit = 100,
    [string]$IMDbExportsPath,
    [switch]$UpdateImdbData,
    [switch]$UpdateNativelyData,
    [string]$LogPath
)

Import-Module "$PSScriptRoot/../../modules/tandoku-utils.psm1" -Scope Local
Import-Module "$PSScriptRoot/../../modules/tandoku-log.psm1"

# When -LogPath is supplied, record this script's own warnings and errors
# (including uncaught terminating errors) and forward the same path to each child
# script so a full sync run's diagnostics accumulate into one file. The log is
# reset at the start of each run since the child scripts append to it.
Initialize-TandokuLog -LogPath $LogPath
if ($LogPath -and (Test-Path -LiteralPath $LogPath)) {
    Remove-Item -LiteralPath $LogPath
}
trap { Write-TandokuLogEntry 'ERROR' $_; break }

$logArgs = @{}
if ($LogPath) {
    $logArgs['LogPath'] = $LogPath
}

RequireCommand yq

Write-Host "Setting console encoding to UTF-8 (required for yq roundtripping)"
[Console]::InputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()

$dbPath = "$Path/films.yaml"
$dbJsonPath = "$Path/films.json"
$sources = "$Path/sources"

& "$PSScriptRoot/ImportNetflixWatchlist.ps1" -Path "$sources/netflix/netflix-my-list.json" -DatabasePath $dbPath @logArgs

if ($NetflixCatalog) {
    & "$PSScriptRoot/ImportNetflixCatalog.ps1" -DatabasePath $dbPath -CachePath "$sources/netflix" -RequestLimit $NetflixRequestLimit @logArgs
}

if ($IMDbExportsPath) {
    & "$PSScriptRoot/DownloadIMDbLists.ps1" -IMDbExportsPath $IMDbExportsPath -CsvPath "$sources/imdb/lists" @logArgs
}
& "$PSScriptRoot/ImportIMDbList.ps1" -DatabasePath $dbPath -CsvPath "$sources/imdb/lists" @logArgs

& "$PSScriptRoot/PopulateWikidata.ps1" -DatabasePath $dbPath -Force:$Force @logArgs

& "$PSScriptRoot/PopulateIMDb.ps1" -DatabasePath $dbPath -ImdbDataPath "$sources/imdb" -UpdateImdbData:$UpdateImdbData @logArgs

& "$PSScriptRoot/PopulateNatively.ps1" -DatabasePath $dbPath -NativelyDataPath "$sources/natively" -UpdateNativelyData:$UpdateNativelyData -NativelyLanguage ja -OriginalLanguage ja @logArgs

& "$PSScriptRoot/ExportFilms.ps1" -DatabasePath $dbPath -OutputPath $dbJsonPath @logArgs
