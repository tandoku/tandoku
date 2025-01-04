param(
    [Parameter(Mandatory=$true)]
    [String[]]
    $Path,

    [Parameter()]
    [String]
    $FileName,

    [Parameter(Mandatory=$true)]
    [ValidateSet('text', 'binary', 'ignore')]
    [String]
    $VersionControl,

    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

# TODO - change tandoku command to tandoku volume source add
$sourceImportArgs = ArgsToArray source import
$sourceImportArgs += $Path
if ($FileName) {
    $sourceImportArgs += ArgsToArray -n $FileName
}
if ($VolumePath) {
    $sourceImportArgs += ArgsToArray --volume $VolumePath
}

$result = InvokeTandokuCommand $sourceImportArgs
if ($result) {
    TandokuVersionControlAdd -Path $result -Kind $VersionControl
    return (Get-Item -LiteralPath $result)
}