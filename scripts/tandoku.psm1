$tandokuScripts = (Split-Path $MyInvocation.MyCommand.Path -Parent)
$tandokuModules = "$tandokuScripts/tandoku*.psm1"
$tandokuRootModule = "$tandokuScripts/tandoku.psm1"
$tandokuSecondaryModules = "$tandokuScripts/tandoku-*.psm1"

$tandokuRepoRoot = (Split-Path $tandokuScripts -Parent)
if (Test-Path "$tandokuRepoRoot/src/cli/bin/Debug/net6.0/tandoku.exe") {
    Set-Alias tandoku "$tandokuRepoRoot/src/cli/bin/Debug/net6.0/tandoku.exe"
} elseif (Test-Path "$tandokuRepoRoot/src/cli/bin/Release/net6.0/tandoku.exe") {
    Set-Alias tandoku "$tandokuRepoRoot/src/cli/bin/Release/net6.0/tandoku.exe"
}

# Consider adding the scripts directory to $env:PSModulePath rather than loading secondary modules upfront
# OR declare secondary modules as dependencies (see tip under -Global parameter at https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.core/import-module?view=powershell-7.1&WT.mc_id=ps-gethelp#parameters)
Get-ChildItem $tandokuSecondaryModules | Import-Module -Scope Global

function Reset-TandokuModules {
    Get-ChildItem $tandokuSecondaryModules | Import-Module -Scope Global -Force
}

Export-ModuleMember -Function *-* -Alias *
