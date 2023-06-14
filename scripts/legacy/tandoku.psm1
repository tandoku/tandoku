$tandokuScripts = (Split-Path $MyInvocation.MyCommand.Path -Parent)
$tandokuModules = "$tandokuScripts/tandoku*.psm1"
$tandokuRootModule = "$tandokuScripts/tandoku.psm1"
$tandokuSecondaryModules = "$tandokuScripts/tandoku-*.psm1"

$tandokuRepoRoot = (Split-Path $tandokuScripts -Parent)
$tandokuCliTargetFramework = 'net7.0'
$tandokuCliBin = 'src/Tandoku.CommandLine/bin'
if (Test-Path "$tandokuRepoRoot/$tandokuCliBin/Debug/$tandokuCliTargetFramework/tandoku.exe") {
    New-Alias tandoku "$tandokuRepoRoot/$tandokuCliBin/Debug/$tandokuCliTargetFramework/tandoku.exe"
} elseif (Test-Path "$tandokuRepoRoot/$tandokuCliBin/Release/$tandokuCliTargetFramework/tandoku.exe") {
    New-Alias tandoku "$tandokuRepoRoot/$tandokuCliBin/Release/$tandokuCliTargetFramework/tandoku.exe"
}

# Consider adding the scripts directory to $env:PSModulePath rather than loading secondary modules upfront
# OR declare secondary modules as dependencies (see tip under -Global parameter at https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.core/import-module?view=powershell-7.1&WT.mc_id=ps-gethelp#parameters)
Get-ChildItem $tandokuSecondaryModules | Import-Module -Scope Global

function Reset-TandokuModules {
    # TODO: use Remove-Module to first remove modules
    # (removes nested modules & classes, which Import-Module -Force doesn't do)
    Get-ChildItem $tandokuSecondaryModules | Import-Module -Scope Global -Force
}

function Get-TandokuRepoRoot {
    return $tandokuRepoRoot
}

Export-ModuleMember -Function *-* -Alias *
