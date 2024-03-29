using namespace System.Collections.Generic

$tandokuLibraryMetadataPath = $null

enum SourceControlKind {
    None
    Git
}

enum BlobStoreKind {
    None
    Dvc
}

function Initialize-TandokuLibrary {
    param(
        [Parameter()]
        [String]
        $Path = '.',

        [Parameter()]
        [String]
        $Name = 'library',

        [Parameter()]
        [String]
        $Language = 'ja',

        [Parameter()]
        [String]
        $ReferenceLanguage = 'en',

        [Parameter()]
        [SourceControlKind]
        $SourceControl = [SourceControlKind]::None,

        [Parameter()]
        [BlobStoreKind]
        $BlobStore = [BlobStoreKind]::None,

        # TODO: remove BlobStorePath (deprecated)
        [Parameter()]
        [String]
        $BlobStorePath,

        [Parameter()]
        [String]
        $ExternalStagingPath,

        [Parameter()]
        [String]
        $KindleDevicePath
    )

    $metadataPath = (Join-Path $Path "$Name.tdkl.yaml")
    $metadataObj = @{
        version = '0.1.0'
        language = $Language
        referenceLanguage = $ReferenceLanguage
    }

    if ($BlobStorePath) {
        $metadataObj.blobStorePath = (Convert-Path $BlobStorePath)
    }

    $metadataObj.config = @{}
    $metadataObj.config.core = @{}

    if ($SourceControl) {
        $metadataObj.config.core.sourceControl = $SourceControlKind.ToString().ToLowerInvariant()
    }

    if ($BlobStore) {
        $metadataObj.config.core.blobStore = $BlobStoreKind.ToString().ToLowerInvariant()
    }

    if ($ExternalStagingPath) {
        $metadataObj.config.core.externalStagingPath = (Convert-Path $ExternalStagingPath)
    }

    if ($KindleDevicePath) {
        $metadataObj.config.kindle = @{
           devicePath = (Convert-Path $KindleDevicePath)
        }
    }

    if ($metadataObj.config.core.count -eq 0) {
        $metadataObj.config.Remove('core')
    }
    if ($metadataObj.config.count -eq 0) {
        $metadataObj.Remove('config')
    }

    WriteMetadataContent $metadataPath $metadataObj
}

function WriteMetadataContent($metadataPath, $metadataObj) {
    ConvertTo-Yaml $metadataObj | Set-Content $metadataPath
}

function ReadMetadataContent($metadataPath) {
    Get-Item $metadataPath |
        Get-Content |
        ConvertFrom-Yaml
}

function Register-TandokuLibrary {
    param(
        [Parameter(Mandatory=$true)]
        [String]
        $Path
    )

    if ($Path -like '*.tdkl.yaml') {
        $script:tandokuLibraryMetadataPath = (Convert-Path $Path)
    } elseif (Test-Path $Path -PathType Container) {
        $metadataPath = Get-Item (Join-Path $Path '*.tdkl.yaml')
        if ($metadataPath.Count -ne 1) {
            throw "Expecting single tandoku library (.tdkl.yaml) at $Path"
        }
        $script:tandokuLibraryMetadataPath = (Convert-Path $metadataPath)
    } else {
        throw "Not a valid tandoku library: $Path"
    }

    return Get-TandokuLibrary
}

function Get-TandokuLibrary {
    if (-not $script:tandokuLibraryMetadataPath) {
        throw 'Register tandoku library first'
    }

    $metadataPath = $script:tandokuLibraryMetadataPath
    $m = ReadMetadataContent $metadataPath
    return [PSCustomObject] @{
        Path = (Split-Path $metadataPath -Parent)
        MetadataPath = $metadataPath
        BlobPath = $m.blobStorePath
        Config = $m.config
        Language = $m.language
        ReferenceLanguage = $m.referenceLanguage
    }
}

function Get-TandokuSourceControl {
    $lib = Get-TandokuLibrary
    return [SourceControlKind] ($lib.config.core.sourceControl ?? [SourceControlKind]::None)
}

function Get-TandokuBlobStore {
    $lib = Get-TandokuLibrary
    return [BlobStoreKind] ($lib.config.core.blobStore ?? [BlobStoreKind]::None)
}

function Get-TandokuExternalStagingPath {
    $lib = Get-TandokuLibrary
    return $lib.config.core.externalStagingPath
}

function Get-TandokuLibraryPath {
    param(
        [Parameter()]
        [String]
        $LiteralPath,

        [Parameter()]
        [Switch]
        $Blob, #TODO: mutually exclusive with $Relative

        [Parameter()]
        [Switch]
        $Relative,

        [Parameter()]
        [Switch]
        $NoResolve
    )

    $lib = Get-TandokuLibrary

    if ($Blob) {
        $rootPath = $lib.BlobPath

        if (-not $rootPath) {
            return $null
        }
    } else {
        $rootPath = $lib.Path
    }

    if ($LiteralPath) {
        # Treat paths starting with '.' as relative to working directory
        # (otherwise relative paths are considered relative to library root)
        # TODO: consider separating these into separate properties or have a switch
        # to control this behavior (e.g. RelativeToLibraryRoot or something)
        if ($LiteralPath -like '.*') {
            $LiteralPath = Resolve-Path -LiteralPath $LiteralPath
        }

        if (Split-Path $LiteralPath -IsAbsolute) {
            $LiteralPath = Convert-Path -LiteralPath $LiteralPath

            $relativePath = ExtractRelativePath $lib.Path $LiteralPath
            if (-not $relativePath -and $lib.BlobPath) {
                $relativePath = ExtractRelativePath $lib.BlobPath $LiteralPath
            }

            if (-not $relativePath) {
                throw "Specified path is not a library path: $LiteralPath"
            }
        } else {
            $relativePath = $LiteralPath
        }

        if ($Relative) {
            return $relativePath
        } else {
            $resolve = -not $NoResolve
            return (Join-Path $rootPath $relativePath -Resolve:$resolve)
        }
    } else {
        if ($Relative) {
            return '.'
        } else {
            return $rootPath
        }
    }
}

function Set-LocationToTandokuLibraryPath {
    param(
        [Parameter()]
        [String]
        $Path,

        [Parameter()]
        [Switch]
        $Blob
    )

    if (-not $Path) {
        $Path = $PWD
    }
    $newPath = Get-TandokuLibraryPath $Path -Blob:$Blob
    if ($newPath) {
        Set-Location (MapToPSDriveAlias $newPath)
    }
}
New-Alias gotolib Set-LocationToTandokuLibraryPath

function Set-LocationToTandokuBlobPath {
    param(
        [Parameter()]
        [String]
        $Path
    )
    Set-LocationToTandokuLibraryPath $Path -Blob
}
New-Alias gotoblob Set-LocationToTandokuBlobPath

function Set-LocationToTandokuLibraryRoot {
    Set-LocationToTandokuLibraryPath /
}
New-Alias tdklib Set-LocationToTandokuLibraryRoot

function Set-LocationToTandokuBlobRoot {
    Set-LocationToTandokuBlobPath /
}
New-Alias tdkblob Set-LocationToTandokuBlobRoot

function New-TandokuVolume {
    param(
        [Parameter(Mandatory=$true)]
        [String]
        $Title,

        [Parameter()]
        [String]
        $ContainerPath,

        [Parameter()]
        [String]
        $Moniker,

        [Parameter()]
        [String[]]
        $Tags,

        [Parameter()]
        [String]
        $ContentOrigin,

        [Parameter()]
        [String]
        $ContentOriginUrl,

        [Parameter()]
        [String]
        $LicenseOrigin,

        [Parameter()]
        [String]
        $LicenseOriginUrl,

        [Parameter()]
        [String]
        $OriginalMedia,

        [Parameter()]
        [ValidateSet('film', 'nintendo-switch-album')]
        [String]
        $Template,

        [Parameter()]
        [Switch]
        $Force
    )

    $props = ProcessTandokuVolumePropertyParameters $PSBoundParameters

    $volumeFSName = $props.FSName()
    $volumePath = Join-Path (Get-TandokuLibraryPath $props.ContainerPath) $volumeFSName

    $blobContainerPath = Get-TandokuLibraryPath $props.ContainerPath -Blob
    if ($blobContainerPath) {
        $volumeBlobPath = Join-Path $blobContainerPath $volumeFSName
    } else {
        $volumeBlobPath = $null
    }

    if (Test-Path $volumePath) {
        if (-not $Force) {
            throw "$volumePath already exists"
        }
    } else {
        [void](New-Item $volumePath -Type Directory)
    }

    if ($volumeBlobPath) {
        if (Test-Path $volumeBlobPath) {
            if (-not $Force) {
                throw "$volumeBlobPath already exists"
            }
        } else {
            [void](New-Item $volumeBlobPath -Type Directory)
        }
    }

    $metadataPath = (Join-Path $volumePath "$volumeFSName.tdkv.yaml")
    SetTandokuVolumeMetadataProperties $metadataPath $props

    $v = Get-TandokuVolume $metadataPath
    CreateTandokuVolumeDirs $v $props

    Add-TandokuVolumeToSourceControl $v -Commit

    return $v
}
New-Alias ntv New-TandokuVolume

function Set-TandokuVolumeProperties {
    param(
        [Parameter()] #TODO: mutually exclusive with $Path
        [PSCustomObject] #TODO: TandokuVolume
        $InputObject,

        [Parameter()]
        [String]
        $Path,

        [Parameter()]
        [String]
        $Title,

        [Parameter()]
        [String]
        $ContainerPath,

        [Parameter()]
        [String]
        $Moniker,

        [Parameter()]
        [String[]]
        $Tags,

        [Parameter()]
        [String]
        $ContentOrigin,

        [Parameter()]
        [String]
        $ContentOriginUrl,

        [Parameter()]
        [String]
        $LicenseOrigin,

        [Parameter()]
        [String]
        $LicenseOriginUrl,

        [Parameter()]
        [String]
        $OriginalMedia,

        [Parameter()]
        [ValidateSet('film', 'nintendo-switch-album')]
        [String]
        $Template
    )

    if ($Path) {
        $InputObject = Get-TandokuVolume -Path $Path
    }
    if (-not $InputObject) {
        Write-Error "No input object"
        return
    }

    $metadataPath = $InputObject.MetadataPath
    $props = ProcessTandokuVolumePropertyParameters $PSBoundParameters
    SetTandokuVolumeMetadataProperties $metadataPath $props
    CreateTandokuVolumeDirs $InputObject $props

    # Process changes to file system if Title/Moniker/ContainerPath changed
    if ($props.Title -or $props.ContainerPath -or $props.Moniker) {
        if (-not $props.Title) { $props.Title = $InputObject.Title }
        if (-not $props.ContainerPath) { $props.ContainerPath = $InputObject.ContainerPath }
        if (-not $props.Moniker) { $props.Moniker = $InputObject.Moniker }

        $oldFSName = $InputObject.FSName
        $newFSName = $props.FSName()

        if ($oldFSName -ne $newFSName) {
            Get-ChildItem "$oldFSName.*" -Recurse |
                Foreach-Object {
                    if ($_ -match "^(.+)$oldFSName(.+)$") {
                        $newPath = "$($Matches[1])$newFSName$($Matches[2])"
                        Move-Item $_ $newPath
                    } else {
                        Write-Warning "$_ did not match pattern and was not renamed"
                    }
                }
        }

        $oldPath = $InputObject.Path
        $newPath = Join-Path (Get-TandokuLibraryPath $props.ContainerPath) $newFSName
        if ($oldPath -ne $newPath) {
            Move-Item $oldPath $newPath
        }

        $oldBlobPath = $InputObject.BlobPath
        if ($oldBlobPath) {
            $newBlobPath = Join-Path (Get-TandokuLibraryPath $props.ContainerPath -Blob) $newFSName
            if ($oldBlobPath -ne $newBlobPath) {
                Move-Item $oldBlobPath $newBlobPath
            }
        }
    }
}
New-Alias stvp Set-TandokuVolumeProperties

class TandokuVolumeProperties {
    [String] $Title
    [String] $ContainerPath
    [String] $Moniker
    [String[]] $Tags

    [String[]] $LibDirs
    [String[]] $BlobDirs

    [String] $ContentOrigin
    [String] $ContentOriginUrl
    [String] $LicenseOrigin
    [String] $LicenseOriginUrl

    [String] $OriginalMedia

    [string] FSName() {
        $volumeFSName = CleanInvalidPathChars $this.Title
        if ($this.Moniker) {
            $volumeFSName = "$($this.Moniker) $volumeFSName"
        }
        return $volumeFSName
    }
}

function ProcessTandokuVolumePropertyParameters([Hashtable] $params) {
    if ($params.Template) {
        $template = $params.Template
        $params.Remove('Template')
    }

    # TODO: remove other extraneous parameters
    # (should be able to get this from Reflection on TandokuVolumeProperties)
    if ($params.Path) { $params.Remove('Path') }
    if ($params.Force) { $params.Remove('Force') }

    $props = [TandokuVolumeProperties] $params

    if ($template) {
        $templateProps = GetTandokuVolumeTemplateProperties $template
        $props = CombineTandokuVolumeProperties $props,$templateProps
    }

    return $props
}

function SetTandokuVolumeMetadataProperties([String] $metadataPath, [TandokuVolumeProperties] $props) {
    if (Test-Path $metadataPath) {
        $metadataObj = ReadMetadataContent $metadataPath
    } else {
        $metadataObj = @{ version = '0.1.0' }
    }

    if ($props.Title) { $metadataObj.title = $props.Title }
    if ($props.Moniker) { $metadataObj.moniker = $props.Moniker }
    if ($props.Tags) { $metadataObj.tags = $props.Tags } #TODO: this won't clear tags if empty Tags array specified

    $sourceObj = $metadataObj.source ?? @{}

    if ($props.ContentOrigin -or $props.ContentOriginUrl) {
        if (-not $sourceObj.contentOrigin) { $sourceObj.contentOrigin = @{} }
        if ($props.ContentOrigin) {
            $sourceObj.contentOrigin.provider = $props.ContentOrigin
        }
        if ($props.ContentOriginUrl) {
            $sourceObj.contentOrigin.url = $props.ContentOriginUrl
        }
    }
    if ($props.LicenseOrigin -or $props.LicenseOriginUrl) {
        if (-not $sourceObj.licenseOrigin) { $sourceObj.licenseOrigin = @{} }
        if ($props.LicenseOrigin) {
            $sourceObj.licenseOrigin.provider = $props.LicenseOrigin
        }
        if ($props.LicenseOriginUrl) {
            $sourceObj.licenseOrigin.url = $props.LicenseOriginUrl
        }
    }

    if ($props.OriginalMedia) { $sourceObj.originalMedia = $props.OriginalMedia }

    if ($sourceObj.Count -gt 0) {
        $metadataObj.source = $sourceObj
    }

    WriteMetadataContent $metadataPath $metadataObj
}

#TODO: PSCustomObject -> TandokuVolume
function CreateTandokuVolumeDirs([PSCustomObject] $volume, [TandokuVolumeProperties] $props) {
    $dirConfigs = @(
        @{ Dirs = $props.LibDirs; RootPath = $volume.Path },
        @{ Dirs = $props.BlobDirs; RootPath = ($volume.BlobPath ?? $volume.Path) })

    $tokenMap = @{
        '$lang' = $volume.Language
        '$reflang' = $volume.ReferenceLanguage
    }

    foreach ($c in $dirConfigs) {
        $c.Dirs | Foreach-Object {
            $relPath = ReplaceTokensInString $_ $tokenMap
            CreateDirectoryIfNotExists (Join-Path $c.RootPath $relPath)
        }
    }
}

#TODO: move to utils
function ReplaceTokensInString([String] $s, [Hashtable] $map) {
    #TODO: rewrite this to a single regex replace (match any of $map keys)
    foreach ($k in $map.Keys) {
        $kx = [Regex]::Escape($k)
        $s = $s -replace $kx,$map[$k]
    }
    return $s
}

function GetTandokuVolumeTemplateProperties([String] $Template) {
    $props = [TandokuVolumeProperties]::new()
    switch ($Template) {
        'film' {
            $props.ContainerPath = 'films'
            $props.Tags = @('film')
            $props.LibDirs = @('source')
            $props.BlobDirs = @(
                'source',
                'source/$lang',
                'source/$reflang',
                'source/investigate',
                'source/investigate/$lang',
                'source/investigate/$reflang',
                'source/unused',
                'source/unused/$lang',
                'source/unused/$reflang')
        }
        'nintendo-switch-album' {
            $props.ContainerPath = 'nintendo-switch-albums'
            $props.Tags = @('nintendo-switch-album')
        }
    }
    return $props
}

function CombineTandokuVolumeProperties([TandokuVolumeProperties[]] $propsList) {
    if (-not $propsList) {
        return $null
    }

    # NOTE: not merging Title or Moniker (should never be specified by template)
    $simplePropNames = @(
        'LibDirs',
        'BlobDirs',
        'ContainerPath',
        'ContentOrigin',
        'ContentOriginUrl',
        'LicenseOrigin',
        'LicenseOriginUrl',
        'OriginalMedia'
    )

    $props = $propsList[0]

    for ($i = 1; $i -lt $propsList.Count; $i++) {
        $mergeProps = $propsList[$i]

        foreach ($p in $simplePropNames) {
            if ($mergeProps.$p -and -not $props.$p) {
                $props.$p = $mergeProps.$p
            }
        }

        if ($mergeProps.Tags) {
            if ($newTags) {
                $newTags = [HashSet[string]] $props.Tags
                $newTags.UnionWith($mergeProps.Tags)
                $props.Tags = $newTags.ToArray()
            } else {
                $props.Tags = $mergeProps.Tags
            }
        }
    }

    return $props
}

function Get-TandokuVolume {
    param(
        [Parameter()]
        [String]
        $Path,

        [Parameter()]
        [String]
        $Moniker,

        [Parameter()]
        [String[]]
        $Tags
    )

    $lib = Get-TandokuLibrary
    
    if (-not $Path) {
        $Path = $lib.Path
    }

    Get-ChildItem $Path -Filter *.tdkv.yaml -Recurse |
        Foreach-Object {
            $volumePath = Split-Path $_ -Parent
            $m = ReadMetadataContent $_
            [PSCustomObject] @{
                Title = $m.title
                Moniker = $m.moniker
                Language = $m.language ?? $lib.Language
                ReferenceLanguage = $m.referenceLanguage ?? $lib.ReferenceLanguage
                Tags = $m.tags
                FSName = Split-Path $volumePath -Leaf
                ContainerPath = (Get-TandokuLibraryPath (Split-Path $volumePath -Parent) -Relative)
                Path = $volumePath
                MetadataPath = [String] $_
                BlobPath = (Get-TandokuLibraryPath $volumePath -Blob)
                Metadata = $m
            }
        } |
        Where-Object {
            if ($Moniker) {
                if ($_.Moniker -notlike $Moniker) {
                    return $false
                }
            }

            if ($Tags) {
                foreach ($tag in $Tags) {
                    if ($_.Tags -notcontains $tag) {
                        return $false
                    }
                }
            }

            return $true
        }
}
New-Alias gtv Get-TandokuVolume

function Update-TandokuVolume {
    param(
        # TODO: multiple parameter sets to allow calling this with $Path or similar
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        $InputObject,

        [Parameter()]
        [Switch]
        $Force
    )
    process {
        if ($InputObject.Tags -contains 'nintendo-switch-album') {
            Update-NintendoSwitchAlbumTandokuVolume @PSBoundParameters
        } else {
            Write-Error "Unable to update volume: $($InputObject.Title)"
        }
    }
}
New-Alias utv Update-TandokuVolume

function Export-TandokuVolumeToMarkdown {
    param(
        # TODO: multiple parameter sets to allow calling this with $Path or similar
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        $InputObject
    )
    process {
        $volumePath = $InputObject.Path

        # TODO: tandoku CLI should take volume as input rather than single content file
        $contentPath = Get-Item (Join-Path $volumePath '*.tdkc.yaml')
        if ($contentPath.Count -ne 1) {
            Write-Error "Unable to export volume: $($InputObject.Title)"
            return
        }

        # TODO: export as .tdkv.md rather than .tdkc.md
        $contentFileName = Split-Path $contentPath -Leaf
        $exportFileName = [IO.Path]::ChangeExtension($contentFileName, '.md')
        $exportPath = "$volumePath/export/$exportFileName"

        [void] (tandoku export $contentPath $exportPath --format Markdown)

        return [IO.FileInfo] $exportPath
    }
}

function Compress-TandokuVolume {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param(
        [Parameter(Mandatory=$false, ValueFromPipeline=$true)]
        [ValidateScript({
            if( -Not ($_ | Test-Path) ){
                throw "File or folder does not exist"
            }
            return $true
        })]
        [String] $Path = '.',

        [Switch] $ImagesAsCbz,

        [Switch] $ImageText
    )
    process {
        Get-ChildItem -Path $Path -Filter images -Recurse -Directory |
            Foreach-Object {
                $volumePath = (Split-Path $_ -Parent)
                Push-Location $volumePath

                # TODO: move this to a separate cmdlet?
                $coverPath = 'cover.jpg'
                if (-not (Test-Path $coverPath)) {
                  Get-ChildItem ./images/cover*.jp*g |
                    Foreach-Object {
                      Write-Verbose "Copying $_ to $coverPath"
                      Copy-Item $_ $coverPath
                    }
                } else {
                    Write-Verbose "$(Resolve-Path $coverPath) already exists"
                }

                $title = (Split-Path $volumePath -Leaf)

                if ($ImagesAsCbz) {
                    $cbzPath = "$title.cbz"
                    if ((Test-Path $coverPath) -and (-not (Test-Path $cbzPath))) {
                      Write-Verbose "Creating $cbzPath from images"
                      Compress-Archive ./images/*.* $cbzPath

                      # verify all items added
                      $cbzFileCount = (7z l $cbzPath|sls '(\d+) files$').Matches[0].Groups[1].Value
                      $imgFileCount = (Get-ChildItem ./images/*.*).Count
                      if ($cbzFileCount -eq $imgFileCount) {
                          Write-Verbose "Removing $imgFileCount files from images"
                          Remove-Item ./images/*.*
                      }
                    } elseif (-not (Test-Path $coverPath)) {
                        Write-Verbose "Missing $coverPath, skipping .cbz archive for $title"
                    } else {
                        Write-Verbose "$(Resolve-Path $cbzPath) already exists"
                    }
                }

                if ($ImageText) {
                    $tdzPath = "$title.tdz"
                    if (Test-Path ./images/text/*.*) {
                        Write-Verbose "Copying ./images/text/ to $tdzPath"
                        7z a -spf -tzip $tdzPath images/text/*.*

                        # verify all items added
                        $tdzFileCount = (7z l $tdzPath|sls '(\d+) files$').Matches[0].Groups[1].Value
                        $textFileCount = (Get-ChildItem ./images/text/*.*).Count
                        if ($tdzFileCount -eq $textFileCount) {
                            Write-Verbose "Removing $textFileCount files from images/text"
                            Remove-Item ./images/text/*.*
                        }
                    } else {
                        Write-Verbose "No images/text files found for $title"
                    }
                }

                Pop-Location
            }
    }
}

function Import-TandokuContent {
    param(
        [Parameter(Mandatory=$true)]
        [String]
        $Path,

        [Parameter()]
        [String]
        $Volume,

        [Parameter()]
        [String]
        $TextLanguage = 'ja'
    )

    $resources = (Add-TandokuResource -Path $Path -Volume $Volume -RecognizeText -TextLanguage $TextLanguage)
    $volumeFileName = (Split-Path $Volume -Leaf)
    $contentPath = (Join-Path (Convert-Path $Volume) "$volumeFileName.tdkc.yaml")
    tandoku generate ($resources|Convert-Path) --out $contentPath
}

function Add-TandokuResource {
    param(
        [Parameter(Mandatory=$true)]
        [String]
        $Path,

        [Parameter()]
        [String]
        $Volume,

        [Parameter()]
        [Switch]
        $RecognizeText,

        [Parameter()]
        [String]
        $TextLanguage = 'ja'
    )

    $targetPath = Get-TandokuLibraryPath $Volume -Blob
    # TODO: only support image resources for now
    $targetPath = (Join-Path $targetPath 'images')
    if (-not (Test-Path $targetPath)) {
        [void](mkdir $targetPath)
    }

    $i = 0
    foreach ($item in (Get-ChildItem $Path | Sort-STNumerical)) {
        $i++
        $ext = (Split-Path $item -Extension)
        $targetItemPath = (Join-Path $targetPath "image$($i.ToString('d4'))$ext")
        Copy-Item $item $targetItemPath
        if ($RecognizeText) {
            [void](Add-AcvText $targetItemPath -Language $TextLanguage)
        }

        $targetItemPath
    }
}

Export-ModuleMember -Function *-* -Alias *
