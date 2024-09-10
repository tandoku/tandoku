param(
    [Parameter(Mandatory=$true)]
    [String]
    $InputPath,

    [Parameter(Mandatory=$true)]
    [String]
    $OutputPath,

    [Parameter()]
    $Volume
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-volume.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-films.psm1" -Scope Local

# Prerequisites:
# scoop install python
# pip install subs2cia
# TODO - fork subs2cia repo and merge PR that removes pandas and PR that makes --batch ignore directory
RequireCommand subs2cia

$Volume = ResolveVolume $Volume
if (-not $Volume) {
    return
}
$volumePath = $Volume.Path
$volumeLanguage = $Volume.Definition.Language
$tempPath = "$volumePath/temp"

$tempSubtitlesPath = "$tempPath/subtitles"
CreateDirectoryIfNotExists $tempSubtitlesPath -Clobber

tandoku subtitles generate $InputPath $tempSubtitlesPath

$subtitleFiles = Get-ChildItem "$tempSubtitlesPath/*.*" -Include (GetKnownSubtitleExtensions -FileMask)

$tempMediaPath = "$tempPath/subs2cia-srs"
CreateDirectoryIfNotExists $tempMediaPath -Clobber

foreach ($subtitleFile in $subtitleFiles) {
    $baseName = Split-Path $subtitleFile -LeafBase
    $videoFile = Get-Item "$volumePath/video/$baseName.*" -Include (GetKnownVideoExtensions -FileMask)

    subs2cia srs --inputs $videoFile $subtitleFile `
        --output-dir $tempMediaPath `
        --ignore-none `
        --target-language $volumeLanguage `
        --bitrate 160
}

$media = InvokeTandokuCommand content transform import-subs2cia-media $InputPath $OutputPath `
    --media-path $tempMediaPath `
    --audio-prefix clips/
TandokuImagesImport $media.images -VolumePath $volumePath
TandokuAudioImport $media.audio "$volumePath/audio/clips" -Volume $volume