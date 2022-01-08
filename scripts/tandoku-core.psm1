$tandokuLibraryRoot = $null
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
        $BlobStorePath,

        [Parameter()]
        [String]
        $ExternalStagingPath
    )

    $metadataPath = (Join-Path $Path "$Name.tdkl.yaml")
    $metadataObj = @{
        version = '0.1.0'
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

    ConvertTo-Yaml $metadataObj | Set-Content $metadataPath
}

function Register-TandokuLibrary {
    param(
        [Parameter(Mandatory=$true)]
        [String]
        $Path
    )

    if ((Split-Path $Path -Extension) -eq '.yaml') {
        $leafBase = Split-Path $Path -LeafBase
        if ((Split-Path $leafBase -Extension) -eq '.tdkl') {
            $script:tandokuLibraryMetadataPath = (Convert-Path $Path)
            $script:tandokuLibraryRoot = (Convert-Path (Split-Path $Path -Parent))

            return $script:tandokuLibraryRoot
        }
    }

    if (Test-Path $Path -PathType Container) {
        # TODO: find .tdkl.yaml and load it
        # $metadataPath = Get-Item (Join-Path $Path '*.tdkl.yaml')
    }

    throw "Not a valid tandoku library: $Path"
}

function Get-TandokuLibraryMetadata {
    if (-not $tandokuLibraryMetadataPath) {
        throw 'Register tandoku library first'
    }

    Get-Item $tandokuLibraryMetadataPath |
        Get-Content |
        ConvertFrom-Yaml
}

function Get-TandokuLibraryConfig {
    $m = Get-TandokuLibraryMetadata
    return $m.config
}

function Get-TandokuExternalStagingPath {
    $c = Get-TandokuLibraryConfig
    return $c.core.externalStagingPath
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
        $Moniker
    )

    $libRoot = Get-TandokuLibraryRoot
    $libBlobRoot = Get-TandokuLibraryRoot -Blob
    $volumeFileName = (CleanInvalidPathChars $Title)
    if ($Moniker) {
        $volumeFileName = "$Moniker $volumeFileName"
    }
    $volumePath = (Join-Path $libRoot $ContainerPath $volumeFileName)
    $volumeBlobPath = (Join-Path $libBlobRoot $ContainerPath $volumeFileName)

    if (Test-Path $volumePath) {
        Write-Error "$volumePath already exists"
    } elseif (Test-Path $volumeBlobPath) {
        Write-Error "$volumeBlobPath already exists"
    } else {
        New-Item $volumePath -Type Directory
        [void](New-Item $volumeBlobPath -Type Directory)

        # TODO: create $volumeFileName.tdkv.yaml in $volumePath
        # with title, moniker
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

    $targetPath = (Get-TandokuPath $Volume -Blob)
    # TODO: only support image resources for now
    $targetPath = (Join-Path $targetPath 'images')
    if (-not (Test-Path $targetPath)) {
        [void](mkdir $targetPath)
    }

    $i = 0
    foreach ($item in (Get-ChildItem $Path|Sort-STNumerical)) {
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

function Get-TandokuLibraryRoot {
    param(
        [Parameter()]
        [Switch]
        $Blob
    )

    # TODO: get these from tandoku environment or based on current working directory
    if ($Blob) {
        return 'O:\tandoku\library'
    } else {
        return 'R:\tandoku-library'
    }
}

function Get-TandokuPath {
    param(
        [Parameter(Mandatory=$true)]
        [String]
        $Path,

        [Parameter()]
        [Switch]
        $Blob
    )

    if ($Blob) {
        if ($Path -match '\\tandoku-library\\(.+)$') {
            return (Join-Path 'O:\tandoku\library\' $matches[1])
        }
        throw "Path is not in tandoku library: $Path"
    } else {
        if ($Path -match '\\tandoku\\library\\(.+)$') {
            return (Join-Path 'R:\tandoku-library\' $matches[1])
        }
        throw "Path is not in tandoku library blob store: $Path"
    }
}

Export-ModuleMember -Function *-* -Alias *
