param(
    [Parameter(Mandatory=$true)]
    [String]
    $Path,

    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

CreateDirectoryIfNotExists "$VolumePath/images"
$target = Copy-Item -Path "$Path/*.jpeg" -Destination "$VolumePath/images/" -PassThru
$target += Copy-Item -Path "$Path/*.jpg" -Destination "$VolumePath/images/" -PassThru

TandokuVersionControlAdd -Path $target -Kind binary
