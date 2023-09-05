param(
    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

$volumeRenameArgs = ArgsToArray volume rename
if ($VolumePath) {
    $volumeRenameArgs += ArgsToArray --volume $VolumePath
}

$result = InvokeTandokuCommand $volumeRenameArgs
if ($result) {
    TandokuVersionControlAdd $result.OriginalPath -Kind auto
    TandokuVersionControlAdd $result.RenamedPath -Kind auto
    return $result
}
