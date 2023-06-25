param(
    [Parameter(Mandatory=$true)]
    [String]
    $Path,

    [Parameter()]
    [String]
    $VolumePath
)

# TODO: infer $VolumePath if not specified

[void] (mkdir "$VolumePath/images")
Copy-Item -Path "$Path/*.jpeg" -Destination "$VolumePath/images/"
Copy-Item -Path "$Path/*.jpg" -Destination "$VolumePath/images/"
