param(
    [Parameter()]
    [String]
    $InputPath,

    [Parameter()]
    [String]
    $OutputPath,

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

if (-not $InputPath) {
    $InputPath = "$volumePath/content"
}
if (-not $OutputPath) {
    $OutputPath = "$volumePath/content/linked"
}

$linkedVolumes = $volume.definition.linkedVolumes
foreach ($linkName in $linkedVolumes.Keys) {
    $linkedVolume = $linkedVolumes[$linkName]
    $indexPath = "$($linkedVolume.path)/.tandoku-volume/cache/contentIndex"

    tandoku content link $InputPath $OutputPath --index-path $indexPath --link-name $linkName
}