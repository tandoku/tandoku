param(
    [Parameter(Mandatory=$true)]
    [String]
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

$sourceImportArgs = ArgsToArray source import $Path
if ($FileName) {
    $sourceImportArgs += ArgsToArray -n $FileName
}
if ($VolumePath) {
    $sourceImportArgs += ArgsToArray --volume $VolumePath
}

# TODO: add JSON output instead of string parsing
# also check for error output properly
$tandokuSourceImportOut = (& 'tandoku' $sourceImportArgs)
if ($tandokuSourceImportOut -match 'Added (.+)$') {
    $itemPath = $Matches[1]
    Write-Host $tandokuSourceImportOut
} else {
    Write-Error "Failed to import source file from $Path"
    if ($tandokuSourceImportOut) {
        Write-Error $tandokuSourceImportOut
    }
    return
}

TandokuVersionControlAdd -Path $itemPath -Kind $VersionControl

return (Get-Item -LiteralPath $itemPath)