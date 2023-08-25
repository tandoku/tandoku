using module './tandoku-utils.psm1'

$modulesPath = $PSScriptRoot
$repoRoot = (Split-Path (Split-Path $modulesPath -Parent) -Parent)

function Set-LocationToTandokuRepoRoot {
    Set-Location (MapToPSDriveAlias $repoRoot)
}
New-Alias tdkrepo Set-LocationToTandokuRepoRoot

function Set-LocationToTandokuDocs {
    Set-Location (MapToPSDriveAlias $repoRoot/docs)
}
New-Alias tdkdocs Set-LocationToTandokuDocs

function Set-LocationToTandokuScripts {
    Set-Location (MapToPSDriveAlias $repoRoot/scripts)
}
New-Alias tdkscripts Set-LocationToTandokuScripts

function Set-LocationToTandokuSrc {
    Set-Location (MapToPSDriveAlias $repoRoot/src)
}
New-Alias tdksrc Set-LocationToTandokuSrc

function Set-LocationToTandokuLibrary {
    if ($env:TANDOKU_LIBRARY) {
        Set-Location (MapToPSDriveAlias $env:TANDOKU_LIBRARY)
    } else {
        Write-Error "TANDOKU_LIBRARY environment variable is not defined"
    }
}
New-Alias tdklib Set-LocationToTandokuLibrary

Export-ModuleMember -Function *-* -Alias *
