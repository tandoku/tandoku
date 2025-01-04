param(
    [Parameter()]
    [ValidateSet('epub','markdown')]
    [String]
    $Target = 'epub',

    [Parameter()]
    $Volume
)

Import-Module "$PSScriptRoot/modules/tandoku-volume.psm1" -Scope Local

$Volume = ResolveVolume $Volume
if (-not $Volume) {
    return
}
$volumePath = $Volume.Path

$buildScript = "$volumePath/build.ps1"
if (-not (Test-Path $buildScript)) {
    Write-Error "No custom build script found and default build is not yet implemented"
    $buildScript = $null
}

if ($buildScript) {
    & $buildScript @PSBoundParameters
}