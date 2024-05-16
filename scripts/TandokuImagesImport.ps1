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

$targetDirectory = "$VolumePath/images"

CreateDirectoryIfNotExists $targetDirectory
$target = Copy-Item -Path "$Path/*.jpeg" -Destination "$targetDirectory/" -PassThru
$target += Copy-Item -Path "$Path/*.jpg" -Destination "$targetDirectory/" -PassThru

TandokuVersionControlAdd -Path $targetDirectory -Kind binary
