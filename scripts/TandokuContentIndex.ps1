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

$indexPath = "$volumePath/.tandoku-volume/cache/contentIndex"
CreateDirectoryIfNotExists $indexPath -Clobber

tandoku content index $Path --index-path $indexPath