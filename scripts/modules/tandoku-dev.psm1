using module './tandoku-utils.psm1'

$modulesPath = $PSScriptRoot
$repoRoot = (Split-Path (Split-Path $modulesPath -Parent) -Parent)

function Set-LocationToTandokuRepoRoot {
    Set-Location $repoRoot
}
New-Alias tdkrepo Set-LocationToTandokuRepoRoot

function Set-LocationToTandokuDocs {
    Set-Location $repoRoot/docs
}
New-Alias tdkdocs Set-LocationToTandokuDocs

function Set-LocationToTandokuScripts {
    Set-Location $repoRoot/scripts
}
New-Alias tdkscripts Set-LocationToTandokuScripts

function Set-LocationToTandokuSrc {
    Set-Location $repoRoot/src
}
New-Alias tdksrc Set-LocationToTandokuSrc

function Set-LocationToTandokuLibrary {
    if ($env:TANDOKU_LIBRARY) {
        Set-Location $env:TANDOKU_LIBRARY
    } else {
        Write-Error "TANDOKU_LIBRARY environment variable is not defined"
    }
}
New-Alias tdklib Set-LocationToTandokuLibrary

Export-ModuleMember -Function *-* -Alias *
