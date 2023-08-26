param(
    [Parameter()]
    [ValidateSet('', 'hardlink', 'symlink')]
    [String]
    $CacheType
)

# Prerequisites:
# scoop install dvc

# Note that symlink requires special user privilege on Windows which can be assigned by using the
# official dvc installer (can uninstall and reinstall with scoop afterwards)

dvc init

# TODO - may want to use autostage when managing git operations as well as dvc ones
# dvc config core.autostage true

if ($CacheType) {
    dvc config cache.type "reflink,$CacheType,copy" --local
}

Write-Host "Use `dvc remote add` if needed to set up remote storage"