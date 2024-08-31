function ResolveVolume($volume) {
    if ($volume -is [string] -or -not $volume) {
        return (TandokuVolumeInfo -VolumePath $volume)
    } else {
        return $volume
    }
}