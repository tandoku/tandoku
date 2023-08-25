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
$tandokuVolumeNewOut = (& "tandoku" $volumeNewArgs)
if ($tandokuVolumeNewOut -match ' at (.+)$') {
    $volumePath = $Matches[1]
} else {
    Write-Error "Failed to create new volume"
    Write-Error "$tandokuVolumeNewOut"
    return
}

Write-Host "Created new volume at $volumePath"

# TODO: call tandoku version-control add 

return [PSCustomObject]@{
    VolumePath = $volumePath
}