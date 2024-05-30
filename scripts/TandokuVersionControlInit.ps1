Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

# Prerequisites:
# scoop install dvc
RequireCommand git
RequireCommand dvc

# Set up git if needed
if (-not (Test-Path .git)) {
    git init -b main

    git add .tandoku-library
    git add library.yaml
}

Add-Content .gitignore **/cache/
Add-Content .gitignore **/temp/
Add-Content .gitignore *.epub
Add-Content .gitignore *.html.zip
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