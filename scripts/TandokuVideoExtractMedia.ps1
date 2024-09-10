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
$imagesPath = "$volumePath/images"


$tempSubtitlesPath = "$tempPath/subtitles"
CreateDirectoryIfNotExists $tempSubtitlesPath -Clobber

tandoku subtitles generate $InputPath $tempSubtitlesPath

$videoFiles = Get-ChildItem "$volumePath/video/*.*" -Include (GetKnownVideoExtensions -FileMask)
$subtitleFiles = Get-ChildItem "$tempSubtitlesPath/*.*" -Include (GetKnownSubtitleExtensions -FileMask)

$tempMediaPath = "$tempPath/subs2cia-srs"
#CreateDirectoryIfNotExists $tempMediaPath -Clobber

# TODO - add --batch after forking repo (see above)
# OR iterate over subtitle files and call subs2cia with single subtitle+video file at a time
#subs2cia srs --inputs $videoFiles $subtitleFiles --output-dir $tempMediaPath --ignore-none --target-language $volumeLanguage

# TODO - ImportMedia
#CreateDirectoryIfNotExists $OutputPath
#Copy-Item "$InputPath/*.content.yaml" $OutputPath
$media = InvokeTandokuCommand content transform import-subs2cia-media $InputPath $OutputPath --media-path $tempMediaPath --audio-prefix clips/
TandokuImagesImport $media.images -VolumePath $volumePath
#TandokuAudioImport $media.audio "$volumePath/audio/clips" -VolumePath $volumePath