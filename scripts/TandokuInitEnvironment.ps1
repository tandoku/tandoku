param(
    [Parameter()]
    [Switch]
    $Dev
)

Write-Host "Setting console encoding to UTF-8 (required for tandoku CLI to work)"
[console]::InputEncoding = [console]::OutputEncoding = [System.Text.UTF8Encoding]::new()

$scriptsPath = $PSScriptRoot
if (Test-Path $scriptsPath) {
    Write-Host "Adding tandoku scripts path $scriptsPath to PATH"
    $env:PATH = '{0}{1}{2}' -f $scriptsPath,[IO.Path]::PathSeparator,$env:PATH
} else {
    Write-Warning "Cannot find tandoku scripts path $scriptsPath"
}

$binPath = Join-Path (Split-Path $scriptsPath -Parent) 'src/Tandoku.CommandLine/bin/Debug/net7.0'
if (Test-Path $binPath) {
    Write-Host "Adding tandoku bin path $binPath to PATH"
    $env:PATH = '{0}{1}{2}' -f $binPath,[IO.Path]::PathSeparator,$env:PATH
} else {
    Write-Warning "Cannot find tandoku bin path $binPath"
}

if ($Dev) {
    Import-Module (Join-Path $scriptsPath 'modules/tandoku-dev.psm1')
}
