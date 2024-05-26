param(
    [Parameter(Mandatory=$true, ParameterSetName='Property', Position=0)]
    [String]
    $Property,

    [Parameter(Mandatory=$true, ParameterSetName='Property', Position=1)]
    [String]
    $Value,

    [Parameter(Mandatory=$true, ParameterSetName='Definition')]
    $Definition,

    [Parameter()]
    [String]
    $VolumePath
)

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

if ($Definition) {
    $Definition | ConvertTo-Yaml | Set-Content $volume.definitionPath
    TandokuVersionControlAdd $volume.definitionPath -Kind text
} else {
    tandoku volume set $Property $Value --volume $volumePath
    # TODO: `tandoku volume set` should return affected files rather than hard-coding this
    TandokuVersionControlAdd $volume.definitionPath -Kind text
}