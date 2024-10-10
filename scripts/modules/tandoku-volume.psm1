function ResolveVolume($volume) {
    if ($volume -is [string] -or -not $volume) {
        return (TandokuVolumeInfo -VolumePath $volume)
    } elseif ($volume.Path) {
        return $volume
    } else {
        throw "Invalid volume object: $volume"
    }
}