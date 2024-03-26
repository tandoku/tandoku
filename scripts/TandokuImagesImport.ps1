param(
    [Parameter(Mandatory=$true)]
    [String]
    $Path,

    [Parameter()]
    [String]
    $VolumePath
)

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

if (-not (Test-Path "$VolumePath/images")) {
    [void] (New-Item "$VolumePath/images" -ItemType Directory)
}
$target = Copy-Item -Path "$Path/*.jpeg" -Destination "$VolumePath/images/" -PassThru
$target += Copy-Item -Path "$Path/*.jpg" -Destination "$VolumePath/images/" -PassThru

TandokuVersionControlAdd -Path $target -Kind binary
