[CmdletBinding()]
param(
    [string]$Path = '.',
    [switch]$Force,
    [switch]$UpdateImdbData,
    [switch]$UpdateNativelyData
)

Import-Module "$PSScriptRoot/../../modules/tandoku-utils.psm1" -Scope Local

RequireCommand yq

Write-Host "Setting console encoding to UTF-8 (required for yq roundtripping)"
[Console]::InputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()

$dbPath = "$Path/films.yaml"
$dbJsonPath = "$Path/films.json"
$sources = "$Path/sources"

& "$PSScriptRoot/ImportNetflixWatchlist.ps1" -Path "$sources/netflix/netflix-my-list.json" -DatabasePath $dbPath

& "$PSScriptRoot/PopulateWikidata.ps1" -DatabasePath $dbPath -Force:$Force

& "$PSScriptRoot/PopulateIMDb.ps1" -DatabasePath $dbPath -ImdbDataPath "$sources/imdb" -UpdateImdbData:$UpdateImdbData

& "$PSScriptRoot/PopulateNatively.ps1" -DatabasePath $dbPath -NativelyDataPath "$sources/natively" -UpdateNativelyData:$UpdateNativelyData -NativelyLanguage ja -OriginalLanguage ja

& "$PSScriptRoot/ExportFilms.ps1" -DatabasePath $dbPath -OutputPath $dbJsonPath
