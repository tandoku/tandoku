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
$imageExtensions = GetImageExtensions

CreateDirectoryIfNotExists $targetDirectory
$items = @()
foreach ($imageExtension in $imageExtensions) {
    $items += CopyItemIfNewer -Path "$Path/*$imageExtension" -Destination $targetDirectory -PassThru
}

if ($items) {
    TandokuVersionControlAdd -Path $targetDirectory -Kind binary
}
