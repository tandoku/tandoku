param(
    [Parameter(Mandatory=$true)]
    [String]
    $VolumePath,

    [Parameter()]
    [String]
    $Target,

    [Parameter()]
    [String]
    $Configuration
)

& "$PSScriptRoot/../TandokuInitEnvironment.ps1"

$buildParams = $PSBoundParameters
[void] $buildParams.Remove('VolumePath')

& "$VolumePath/build.ps1" @buildParams
