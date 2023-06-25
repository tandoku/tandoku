# Looks like this loads the module into the process context (so any exported members are visible after script is run)
using module './modules/tandoku-utils.psm1'

param(
    [Parameter(Mandatory=$true)]
    $Path
)

$mappedPath = MapToPSDriveAlias $Path
Write-Host "Mapped path: $mappedPath"
