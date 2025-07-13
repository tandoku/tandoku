param(
    [Parameter()]
    [ValidateSet('epub','markdown')]
    [String]
    $Target,

    [Parameter()]
    $Volume
)

Import-Module "$PSScriptRoot/modules/tandoku-volume.psm1" -Scope Local

$buildParams = $PSBoundParameters

$Volume = ResolveVolume $Volume
if (-not $Volume) {
    return
}
$volumePath = $Volume.Path
$buildParams.Volume = $Volume

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

# TODO - TandokuConfig should support fallback from volume>library>user (or user>volume>library?)
# should not need to specify -Scope Volume here
$volumeConfig = TandokuConfig -Scope Volume -Volume $Volume
$workflowParams = $volumeConfig.'workflow-params'
if ($workflowParams) {
    $buildParams.Params = $workflowParams
}

& $buildScript @buildParams
