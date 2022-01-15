function Sync-NintendoSwitchAlbums {
    param(
        [Parameter()]
        [Switch]
        $SkipDeviceImport,

        [Parameter()]
        [Switch]
        $SkipKindleExport
    )

    if (-not $SkipDeviceImport) {
        Copy-NintendoSwitchDeviceAlbums
    }

    $updatedVolumes = Get-TandokuVolume -Tags nintendo-switch-album |
        Update-TandokuVolume
    
    if ($SkipKindleExport) {
        $updatedVolumes | Export-TandokuVolumeToMarkdown
    } else {
        <# TODO: finish implementing this
        $updatedVolumes | Export-TandokuVolumeToKindle
        Sync-Kindle
        #>
    }

    # TODO: optionally create .cbz
}

function Copy-NintendoSwitchDeviceAlbums($DestinationPath) {
    if (-not $DestinationPath) {
        $DestinationPath = Get-NintendoSwitchStagingPath -Import
    }
    Copy-ShellDriveContents 'Nintendo Switch' $DestinationPath
}

function Get-NintendoSwitchStagingPath {
    param(
        [Parameter()]
        [Switch]
        $Import,

        [Parameter()]
        [Switch]
        $Album
    )

    $lib = Get-TandokuLibrary
    $basePath = $lib.config.'nintendo-switch'.stagingPath

    if (-not $basePath) {
        $extStagingPath = Get-TandokuExternalStagingPath
        $basePath = Join-Path $extStagingPath 'nintendo-switch'
    }

    if ($Import -or $Album) {
        $path = Join-Path $basePath 'import'

        if ($Album) {
            $path = Join-Path $path 'Album'
        }

        return $path
    }
    return $basePath
}

function Update-NintendoSwitchAlbumTandokuVolume {
    param(
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        $InputObject
    )

    # TODO: check volume.config.nintendo-switch-album.title first
    $albumStagingPath = Join-Path (Get-NintendoSwitchStagingPath -Album) $InputObject.Title

    $volumePath = $InputObject.Path
    $volumeBlobPath = $InputObject.BlobPath ?? $volumePath

    # TODO: include a -Force option to regenerate content even if no files added
    # (maybe run Add-AcvText for all files in this case as well?)
    $newItems = Copy-ItemIfNewer $albumStagingPath/*.jpg $volumeBlobPath/images/ -PassThru
    if ($newItems.Count -gt 0) {
        [void] ($newItems | Add-AcvText -Language $InputObject.Language)

        # Create tandoku content from images and generate Markdown file
        $contentFileName = Split-Path $volumePath -Leaf
        $contentFileName = "$contentFileName.tdkc.yaml"
        $contentPath = Join-Path $volumePath $contentFileName
        $contentImages = Get-ChildItem $volumeBlobPath/images/*.jpg
        [void] (tandoku generate $contentImages --out $contentPath)

        return $InputObject
    }
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
