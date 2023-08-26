param(
    # TODO: not mandatory
    [Parameter(Mandatory=$true)]
    [String]
    $Title,

    [Parameter()]
    [String]
    $Path,

    [Parameter()]
    [String]
    $Moniker,

    # TODO: remove? (can be set later instead)
    [Parameter()]
    [String[]]
    $Tags,

    [Parameter()]
    [Switch]
    $Force
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

$volumeNewArgs = ArgsToArray volume new $Title
if ($Path) {
    $volumeNewArgs += ArgsToArray --path $Path
}
if ($Moniker) {
    $volumeNewArgs += ArgsToArray --moniker $Moniker
}
if ($Tags) {
    $volumeNewArgs += ArgsToArray --tags ($Tags -join ',')
}
if ($Force) {
    $volumeNewArgs += '--force'
}

# TODO: add JSON output instead of string parsing
# also check for error output properly
$tandokuVolumeNewOut = (& 'tandoku' $volumeNewArgs)
if ($tandokuVolumeNewOut -match ' at (.+)$') {
    $volumePath = $Matches[1]
    Write-Host $tandokuVolumeNewOut
} else {
    Write-Error "Failed to create new volume"
    if ($tandokuVolumeNewOut) {
        Write-Error $tandokuVolumeNewOut
    }
    return
}

# TODO: these should come from JSON output of `tandoku volume new`
TandokuVersionControlAdd -Path "$VolumePath/.tandoku-volume" -Kind text
TandokuVersionControlAdd -Path "$VolumePath/volume.yaml" -Kind text

return [PSCustomObject]@{
    VolumePath = $volumePath
}