param(
    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

$volumeInfoArgs = ArgsToArray volume info
if ($VolumePath) {
    $volumeInfoArgs += ArgsToArray --volume $VolumePath
}

return (InvokeTandokuCommand $volumeInfoArgs)