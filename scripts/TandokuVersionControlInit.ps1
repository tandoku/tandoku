param(
    [Parameter()]
    [ValidateSet('', 'hardlink', 'symlink')]
    [String]
    $CacheType
)

# Set up git if needed
if (-not (Test-Path .git)) {
    git init -b main

    git add .tandoku-library
    git add library.yaml
}

Add-Content .gitignore **/temp/
git add .gitignore

# Set up dvc

# Prerequisites:
# scoop install dvc

# Note that symlink requires special user privilege on Windows which can be assigned by using the
# official dvc installer (can uninstall and reinstall with scoop afterwards)

dvc init

dvc config core.autostage true
git add .dvc/config

if ($CacheType) {
    dvc config cache.type "reflink,$CacheType,copy" --local
}

Write-Host "Use `dvc remote add` to set up remote storage"