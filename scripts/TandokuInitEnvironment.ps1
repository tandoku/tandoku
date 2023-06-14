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

# TODO: add -Dev switch parameter to add dev aliases (tdksrc, tdkscripts) etc.
