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

# TODO - share imageExtensions across scripts
CreateDirectoryIfNotExists $targetDirectory
Copy-Item -Path "$Path/*.jpeg" -Destination "$targetDirectory/"
Copy-Item -Path "$Path/*.jpg" -Destination "$targetDirectory/"

TandokuVersionControlAdd -Path $targetDirectory -Kind binary
