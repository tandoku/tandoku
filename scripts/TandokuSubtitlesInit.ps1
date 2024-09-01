param(
    [Parameter()]
    [String]
    $InputPath,

    [Parameter()]
    [String]
    $OutputPath,

    [Parameter()]
    [String]
    $Language,

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

if (-not $InputPath) {
    $InputPath = "$($Volume.Path)/source"
}
if (-not $OutputPath) {
    $OutputPath = "$($Volume.Path)/subtitles"
}

$sourceSubtitles = Get-ChildItem "$InputPath/*.*" -Include (GetKnownSubtitleExtensions -FileMask -Language $Language -MatchLanguagePrefix)
if (-not $sourceSubtitles) {
    Write-Warning 'No subtitles found in volume source.'
    return
}

$targetDir = $OutputPath
CreateDirectoryIfNotExists $targetDir
$targetSubtitles = $sourceSubtitles | ForEach-Object {
    $qualifier = ExtractFilmQualifierFromFileName $_
    $extension = Split-Path $_ -Extension
    if ($Language) {
        $extension = ".$Language$extension"
    }
    $fileName = "$($Volume.Slug)$qualifier$extension"
    $targetPath = "$targetDir/$fileName"
    Copy-Item -LiteralPath $_ $targetPath -PassThru
}

if ($targetSubtitles) {
    TandokuVersionControlAdd -Path $targetSubtitles -Kind text
}