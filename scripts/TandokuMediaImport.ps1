param(
    [Parameter(Mandatory=$true)]
    [String]
    $InputPath,

    [Parameter(Mandatory=$true)]
    [String]
    $OutputPath,

    [Parameter(Mandatory=$true)]
    [String]
    $MediaPath,

    [Parameter()]
    $Volume
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-volume.psm1" -Scope Local

$Volume = ResolveVolume $Volume
if (-not $Volume) {
    return
}
$volumePath = $Volume.Path

$importMediaArgs = ArgsToArray content transform import-media $InputPath $OutputPath `
    --media-path $MediaPath `
    --audio-prefix clips/
$media = InvokeTandokuCommand $importMediaArgs
if ($media.images) {
    # TODO - add -RelativeTo $MediaPath to include content folders
    TandokuImagesAdd $media.images -VolumePath $volumePath
}
if ($media.audio) {
    # TODO - add -RelativeTo $MediaPath to include content folders
    TandokuAudioAdd $media.audio "$volumePath/audio/clips" -Volume $volume
}
