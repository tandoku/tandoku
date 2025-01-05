param(
    [Parameter()]
    [ValidateSet('epub','markdown')]
    [String]
    $Target,

    [Parameter()]
    $Volume
)

Import-Module "$PSScriptRoot/modules/tandoku-volume.psm1" -Scope Local

$Volume = ResolveVolume $Volume
if (-not $Volume) {
    return
}
$PSBoundParameters.Volume = $Volume
$volumePath = $Volume.Path

$buildScript = "$volumePath/build.ps1"
if (-not (Test-Path $buildScript)) {
    $workflow = $Volume.Definition.Workflow
    if ($workflow) {
        $buildScript = "$PSScriptRoot/workflows/$workflow.ps1"
        if (-not (Test-Path $buildScript)) {
            Write-Error "Cannot find build script for the specified workflow '$workflow' at $buildScript"
            return
        }
    } else {
        Write-Error 'No custom build script found and no workflow specified in volume definition'
        return
    }
}

& $buildScript @PSBoundParameters