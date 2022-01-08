function Copy-NintendoSwitchDeviceAlbums($DestinationPath) {
    if (-not $DestinationPath) {
        # TODO: get from library config
        $DestinationPath = 'O:\tandoku\external\nintendo-switch\import'
    }
    Copy-ShellDriveContents 'Nintendo Switch' $DestinationPath
}

# Portions copied from and inspired by https://github.com/WillyMoselhy/Weekend-Projects/blob/master/Copy-MTPCameraByMonth.ps1

function Copy-ShellDriveContents($shellDriveName, $targetPath) {
    $shellApp = New-Object -ComObject Shell.Application

    # 17 = ssfDRIVES enum value in ShellSpecialFolderConstants (https://docs.microsoft.com/en-us/windows/win32/api/shldisp/ne-shldisp-shellspecialfolderconstants)
    $drivesItem = $shellApp.NameSpace(17).Self
    $sourceItem = $drivesItem.GetFolder.Items() | Where-Object Name -eq $shellDriveName

    if ($sourceItem) {
        CopyShellFolderContents $shellApp $sourceItem $targetPath
    } else {
        Write-Warning "Could not find specified drive '$shellDriveName'"
    }
}

function CopyShellFolderContents($shellApp, $folderItem, $targetPath) {
    $resolvedTargetPath = (Convert-Path -LiteralPath $targetPath)
    $targetShellFolder = $shellApp.NameSpace($resolvedTargetPath).Self
    if ($targetShellFolder) {
        $folderItem.GetFolder.Items() | Sort-Object -Property Name | Foreach-Object {
            if ($_.IsFolder) {
                $itemPath = Join-Path $targetPath (CleanInvalidPathChars $_.Name)
                if (-not (Test-Path $itemPath)) {
                    New-Item $itemPath -ItemType Directory
                }
                CopyShellFolderContents $shellApp $_ $itemPath
            } else {
                $itemPath = Join-Path $targetPath $_.Name
                if (-not (Test-Path $itemPath)) {
                    $targetShellFolder.GetFolder.CopyHere($_)
                    $itemPath
                }
            }
        }
    } else {
        Write-Warning "Could not find target path '$resolvedTargetPath'"
    }
}

Export-ModuleMember -Function *-* -Alias *
