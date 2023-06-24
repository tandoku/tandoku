param(
    [Parameter(Mandatory=$true)]
    [String]
    $Path,

    [Parameter()]
    [String]
    $VolumePath
)

# TODO: infer $VolumePath if not specified

Copy-Item -Path $Path -Destination "$VolumePath/cover.jpg"