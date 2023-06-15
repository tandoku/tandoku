param(
    [Parameter()]
    [Switch]
    $Dev
)

$scriptsPath = $PSScriptRoot
if (Test-Path $scriptsPath) {
    Write-Host "Adding tandoku scripts path $scriptsPath to PATH"
    $env:PATH = "$scriptsPath;$env:PATH"
} else {
    Write-Warning "Cannot find tandoku scripts path $scriptsPath"
}

$binPath = Join-Path (Split-Path $scriptsPath -Parent) 'src/Tandoku.CommandLine/bin/Debug/net7.0'
if (Test-Path $binPath) {
    Write-Host "Adding tandoku bin path $binPath to PATH"
    $env:PATH = "$binPath;$env:PATH"
} else {
    Write-Warning "Cannot find tandoku bin path $binPath"
}

if ($Dev) {
    Import-Module (Join-Path $scriptsPath 'modules/tandoku-dev.psm1')
}
