param(
    [Parameter()]
    [String]
    $LibraryPath,

    [Parameter()]
    [Switch]
    $Dev
)

if ($IsWindows) {
    Write-Host "Setting console encoding to UTF-8 (required for tandoku CLI to work)"
    [Console]::InputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
}

$scriptsPath = $PSScriptRoot
if (Test-Path $scriptsPath) {
    Write-Host "Adding tandoku scripts path $scriptsPath to PATH"
    $env:PATH = '{0}{1}{2}' -f $scriptsPath,[IO.Path]::PathSeparator,$env:PATH
} else {
    Write-Warning "Cannot find tandoku scripts path $scriptsPath"
}

$binPath = Join-Path (Split-Path $scriptsPath -Parent) 'src/Tandoku.CommandLine/bin/Debug/net9.0'
if (Test-Path $binPath) {
    Write-Host "Adding tandoku bin path $binPath to PATH"
    $env:PATH = '{0}{1}{2}' -f $binPath,[IO.Path]::PathSeparator,$env:PATH
} else {
    Write-Warning "Cannot find tandoku bin path $binPath"
}

if ($LibraryPath) {
    $env:TANDOKU_LIBRARY = (Convert-Path $LibraryPath)
}

# TODO - load public modules on-demand? (move each to its own directory, update PSModulePath)
Import-Module "$scriptsPath/modules/tandoku-public.psm1" -Scope Global
Import-Module "$scriptsPath/modules/tandoku-yaml.psm1" -Scope Global
if ($Dev) {
    Import-Module "$scriptsPath/modules/tandoku-dev.psm1" -Scope Global
}