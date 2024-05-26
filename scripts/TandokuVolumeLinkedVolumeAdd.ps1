param(
    [Parameter(Mandatory=$true, Position=0)]
    [String]
    $LinkName,

    [Parameter(Position=1, ParameterSetName='Path')]
    [String]
    $Path,

    [Parameter(ParameterSetName='Moniker')]
    [String]
    $Moniker,

    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

function ResolveLinkedVolume([string]$Path, [string]$Moniker) {
    if ($Path) {
        $linkedVolumeInfo = TandokuVolumeInfo -VolumePath $Path
        if (-not $linkedVolumeInfo) {
            throw "Cannot find specified linked volume at $Path"
        }
        return $linkedVolumeInfo
    } else {
        throw "TODO: implement resolution by moniker"
    }
}

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

$linkedVolumeInfo = ResolveLinkedVolume $Path $Moniker

if ($volume.definition.linkedVolumes.$LinkName) {
    throw "$LinkName linked volume already exists in volume"
}

$linkedVolume = @{
    path = GetRelativePath $volumePath $linkedVolumeInfo.path
}
if ($linkedVolumeInfo.definition.moniker) {
    $linkedVolume.moniker = $linkedVolumeInfo.definition.moniker
}

if (-not $volume.definition.linkedVolumes) {
    $volume.definition.linkedVolumes = @{}
}
$volume.definition.linkedVolumes.$LinkName = $linkedVolume

TandokuVolumeSet -Definition $volume.definition -VolumePath $volumePath