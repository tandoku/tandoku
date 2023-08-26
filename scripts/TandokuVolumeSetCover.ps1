param(
    [Parameter(Mandatory=$true)]
    [String]
    $Path,

    # TODO: not mandatory (infer if not specified)
    [Parameter(Mandatory=$true)]
    [String]
    $VolumePath
)

$target = Copy-Item -Path $Path -Destination "$VolumePath/cover.jpg" -PassThru
TandokuVersionControlAdd -Path $target -Kind binary