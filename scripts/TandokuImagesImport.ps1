param(
    [Parameter(Mandatory=$true)]
    [String]
    $Path,

    # TODO: not mandatory (infer if not specified)
    [Parameter(Mandatory=$true)]
    [String]
    $VolumePath
)

[void] (mkdir "$VolumePath/images")
$target = Copy-Item -Path "$Path/*.jpeg" -Destination "$VolumePath/images/" -PassThru
$target += Copy-Item -Path "$Path/*.jpg" -Destination "$VolumePath/images/" -PassThru

TandokuVersionControlAdd -Path $target -Kind binary
