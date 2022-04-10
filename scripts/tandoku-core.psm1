using namespace System.Collections.Generic

$tandokuLibraryMetadataPath = $null

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
    }

    if ($BlobStorePath) {
        $metadataObj.blobStorePath = (Convert-Path $BlobStorePath)
    }

    $metadataObj.config = @{}

    if ($ExternalStagingPath) {
        $metadataObj.config.core = @{
           externalStagingPath = (Convert-Path $ExternalStagingPath)
        }
    }

    if ($KindleDevicePath) {
        $metadataObj.config.kindle = @{
           devicePath = (Convert-Path $KindleDevicePath)
        }
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
    }
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
        $Blob
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

        return (Join-Path $rootPath $relativePath -Resolve)
    } else {
        return $rootPath
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
        [ValidateSet('film')]
        [String]
        $Template,

        [Parameter()]
        [Switch]
        $Force
    )

    $props = ProcessTandokuVolumePropertyParameters $PSBoundParameters

    $volumeFSName = CleanInvalidPathChars $props.Title
    if ($props.Moniker) {
        $volumeFSName = "$($props.Moniker) $volumeFSName"
    }
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

    return Get-TandokuVolume $metadataPath
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
        [ValidateSet('film')]
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

    # TODO: process changes to file system if Title/Moniker/ContainerPath changed
    # (refactor as needed from New-TandokuVolume)
}
New-Alias stvp Set-TandokuVolumeProperties

class TandokuVolumeProperties {
    [String] $Title
    [String] $ContainerPath
    [String] $Moniker
    [String[]] $Tags
}

function ProcessTandokuVolumePropertyParameters([Hashtable] $params) {
    if ($params.Template) {
        $template = $params.Template
        $params.Remove('Template')
    }

    # TODO: remove other extraneous parameters

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

    WriteMetadataContent $metadataPath $metadataObj
}

function GetTandokuVolumeTemplateProperties([String] $Template) {
    $props = [TandokuVolumeProperties]::new()
    switch ($Template) {
        'film' {
            $props.ContainerPath = 'films'
        }
    }
    return $props
}

function CombineTandokuVolumeProperties([TandokuVolumeProperties[]] $propsList) {
    if (-not $propsList) {
        return $null
    }

    $props = $propsList[0]

    for ($i = 1; $i -lt $propsList.Count; $i++) {
        $mergeProps = $propsList[$i]
        
        # NOTE: not merging Title or Moniker

        if ($mergeProps.ContainerPath -and -not $props.ContainerPath) {
            $props.ContainerPath = $mergeProps.ContainerPath
        }

        if ($mergeProps.Tags) {
            $newTags = [HashSet[string]] $props.Tags
            $newTags.UnionWith($mergeProps.Tags)
            $props.Tags = $newTags.ToArray()
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
                Language = $m.Language ?? $lib.Language
                Tags = $m.tags
                Path = $volumePath
                MetadataPath = [String] $_
                BlobPath = (Get-TandokuLibraryPath $volumePath -Blob)
            }
        } |
        Where-Object {
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
