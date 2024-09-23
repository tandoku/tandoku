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

# TODO - move this to utils
function GetPropertyNames($obj) {
    if ($obj -is [PSCustomObject]) {
        foreach ($prop in $obj.PSObject.Properties) {
            $prop.Name
        }
    } else {
        foreach ($key in $obj.Keys) {
            $key
        }
    }
}

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
foreach ($linkName in (GetPropertyNames $linkedVolumes)) {
    $linkedVolume = $linkedVolumes.$linkName
    $indexPath = "$($linkedVolume.path)/.tandoku-volume/cache/contentIndex"

    tandoku content link $InputPath $OutputPath --index-path $indexPath --link-name $linkName
}

# TODO TandokuVersionControlAdd for modified files in $OutputPath (should be returned by `tandoku content link`)
# it is a useful part of my workflow though to add files to git staging, run a command, and diff any changes
# against the staged files, so consider adding an override parameter to skip adding to version control
