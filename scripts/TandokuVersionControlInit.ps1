# TODO: Require-Command git, dvc
# Prerequisites:
# scoop install dvc

# Set up git if needed
if (-not (Test-Path .git)) {
    git init -b main

    git add .tandoku-library
    git add library.yaml
}

Add-Content .gitignore **/temp/
git add .gitignore

# Set up dvc

# Note that symlink requires special user privilege on Windows which can be assigned by using the
# official dvc installer (can uninstall and reinstall with scoop afterwards).
# This is only required if cache will be stored on separate partition or drive.

dvc init

dvc config core.autostage true
git add .dvc/config

dvc config cache.type reflink,hardlink,symlink,copy --local

Write-Host "Use `dvc remote add` to set up remote storage"