param(
    [Parameter()]
    $Volume
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-volume.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-films.psm1" -Scope Local

$Volume = ResolveVolume $Volume
if (-not $Volume) {
    return
}

$sourceVideos = Get-ChildItem "$($Volume.Path)/source/*.*" -Include (GetKnownVideoExtensions -FileMask)
if (-not $sourceVideos) {
    Write-Warning 'No videos found in volume source.'
    return
}

$targetDir = "$($Volume.Path)/video"
CreateDirectoryIfNotExists $targetDir
$targetVideos = $sourceVideos | ForEach-Object {
    $qualifier = ExtractFilmQualifierFromFileName $_
    $extension = Split-Path $_ -Extension
    $fileName = "$($Volume.Slug)$qualifier$extension"
    $targetPath = "$targetDir/$fileName"
    if (-not (Test-Path $targetPath)) {
        Copy-Item -LiteralPath $_ $targetPath -PassThru
    } else {
        Write-Warning "$targetPath already exists, skipping"
    }
}

if ($targetVideos) {
    TandokuVersionControlAdd -Path $targetVideos -Kind binary
}