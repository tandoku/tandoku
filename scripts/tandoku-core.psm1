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
        $ExternalStagingPath
    )

    $metadataPath = (Join-Path $Path "$Name.tdkl.yaml")
    $metadataObj = @{
        version = '0.1.0'
        language = $Language
    }

    if ($BlobStorePath) {
        $metadataObj.blobStorePath = (Convert-Path $BlobStorePath)
    }

    if ($ExternalStagingPath) {
        $metadataObj.config = @{
            core = @{
               externalStagingPath = (Convert-Path $ExternalStagingPath)
            }
        }
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

        return (Join-Path $rootPath $relativePath)
    } else {
        return $rootPath
    }
}

function ExtractRelativePath($basePath, $childPath) {
    $basePathWithSep = Join-Path $basePath /
    if ($childPath -like "$basePathWithSep*") {
        return $childPath.Substring($basePathWithSep.length)
    } else {
        return $null
    }
}

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
        [Switch]
        $Force
    )

    $volumeFSName = (CleanInvalidPathChars $Title)
    if ($Moniker) {
        $volumeFSName = "$Moniker $volumeFSName"
    }
    $volumePath = Join-Path (Get-TandokuLibraryPath $ContainerPath) $volumeFSName

    $blobContainerPath = Get-TandokuLibraryPath $ContainerPath -Blob
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

    $metadataObj = @{
        version = '0.1.0'
        title = $Title
    }

    if ($Moniker) {
        $metadataObj.moniker = $Moniker
    }

    if ($Tags) {
        $metadataObj.tags = $Tags
    }

    WriteMetadataContent $metadataPath $metadataObj

    return Get-TandokuVolume $metadataPath
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

function Update-TandokuVolume {
    param(
        # TODO: multiple parameter sets to allow calling this with $Path or similar
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        $InputObject
    )
    process {
        # TODO: consider using splatting (see about_Splatting) to forward parameters to child functions
        if ($InputObject.Tags -contains 'nintendo-switch-album') {
            Update-NintendoSwitchAlbumTandokuVolume $InputObject
        } else {
            Write-Error "Unable to update volume: $($InputObject.Title)"
        }
    }
}

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

        $contentFileName = Split-Path $contentPath -Leaf
        $exportFileName = [IO.Path]::ChangeExtension($contentFileName, '.md')

        tandoku export $contentPath $volumePath\export\$exportFileName --format Markdown
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
