function Export-TandokuVolumeToKindle {
    param(
        # TODO: multiple parameter sets to allow calling this with $Path or similar
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        $InputObject
    )
    process {
        $volumeTitle = $InputObject.Title
        $volumePath = $InputObject.Path
        $volumeBlobPath = $InputObject.BlobPath ?? $volumePath

        $markdownPath = Export-TandokuVolumeToMarkdown $InputObject
        $markdownFileName = Split-Path $markdownPath -Leaf

        $tempDir = New-TempDirectory 'tandoku'
        $volumeTempPath = $tempDir.TempDirPath
        try {
            # Copy necessary files/folder structure to temp path
            Copy-Item $markdownPath $volumeTempPath/
            if (Test-Path $volumeBlobPath/images) {
                $imagesTempPath = New-Item $volumeTempPath/images -ItemType Directory
                Copy-Item $volumeBlobPath/images/*.* $imagesTempPath
            }

            $kindleTempPath = ConvertTo-KindleBook $volumeTempPath/$markdownFileName -TargetFormat azw3 -Title $volumeTitle
            $kindleFileName = Split-Path $kindleTempPath -Leaf
            $kindleExportPath = "$volumeBlobPath/export/$kindleFileName"
            if (Test-Path $kindleExportPath) {
                Remove-Item $kindleExportPath
            }
            Move-Item $kindleTempPath $kindleExportPath

            $kindleStagingPath = Join-Path (Get-KindleStagingPath -TandokuDocumentExport) $kindleFileName
            Copy-Item $kindleExportPath $kindleStagingPath
        }
        finally {
            $tempDir.Dispose()
        }
    }
}

function Sync-Kindle {
    param(
        [Parameter()]
        [Switch]
        $NoExport,

        [Parameter()]
        [Switch]
        $NoImport
    )

    $deviceRootPath = Get-KindleDevicePath
    if (-not (Test-Path $deviceRootPath)) {
        Write-Error "Kindle device not available at $deviceRootPath"
        return
    }

    if (-not $NoExport) {
        $kindleExportPath = Get-KindleStagingPath -TandokuDocumentExport
        if (Test-Path $kindleExportPath) {
            $kindleDevicePath = Get-KindleDevicePath -TandokuDocuments
            CreateDirectoryIfNotExists $kindleDevicePath
            Copy-ItemIfNewer $kindleExportPath/*.azw3 $kindleDevicePath/ -Force -PassThru
        }
    }

    if (-not $NoImport) {
        $kindleImportPath = Get-KindleStagingPath -Import
        $importFilePaths = @(
            'documents/My Clippings.txt',
            'system/vocabulary/vocab.db'
        )
        foreach ($filePath in $importFilePaths) {
            $sourcePath = Join-Path $deviceRootPath $filePath
            $targetPath = Join-Path $kindleImportPath $filePath
            CreateDirectoryIfNotExists (Split-Path $targetPath -Parent)
            Copy-ItemIfNewer $sourcePath $targetPath -Force -PassThru
        }
    }
}

function Get-KindleStagingPath {
    param(
        [Parameter()]
        [Switch]
        $TandokuDocumentExport,

        [Parameter()]
        [Switch]
        $Import
    )

    $lib = Get-TandokuLibrary
    $basePath = $lib.config.kindle.stagingPath

    if (-not $basePath) {
        $extStagingPath = Get-TandokuExternalStagingPath
        $basePath = Join-Path $extStagingPath 'kindle'
    }

    if ($TandokuDocumentExport) {
        return Join-Path $basePath 'export/documents/tandoku'
    } elseif ($Import) {
        return Join-Path $basePath 'import'
    }
    return $basePath
}

function Get-KindleDevicePath {
    param(
        [Parameter()]
        [Switch]
        $TandokuDocuments
    )

    $lib = Get-TandokuLibrary
    $basePath = $lib.config.kindle.devicePath

    if (-not $basePath) {
        throw "Kindle device path not configured"
    }

    if ($TandokuDocuments) {
        return Join-Path $basePath 'documents/tandoku'
    }
    return $basePath
}

# cp 'e:\documents\My Clippings.txt' o:\Tandoku\Kindle\raw
# cp 'e:\system\vocabulary\vocab.db' o:\Tandoku\Kindle\raw

function Copy-KindleMetadataCache {
    $dt = [DateTime]::Today.ToString('yyyy-MM-dd')
    Copy-Item "$env:LOCALAPPDATA\Amazon\Kindle\Cache\KindleSyncMetadataCache.xml" "KindleSyncMetadataCache-$dt.xml"
}

function Get-KindleBook {
    $x = [xml] (Get-Content "$env:LOCALAPPDATA\Amazon\Kindle\Cache\KindleSyncMetadataCache.xml")
    $x.response.add_update_list.meta_data |
        Foreach-Object {
            [PSCustomObject] @{
                ASIN = $_.ASIN
                Title = $_.title.'#text'
                TitlePronunciation = $_.title.pronunciation
                Authors = ($_.authors.author |
                    Foreach-Object {
                        [PSCustomObject] @{
                            Author = $_.'#text'
                            AuthorPronunciation = $_.pronunciation
                        }
                    })
                Publishers = $_.publishers.publisher
                PublicationDate = $_.publication_date
                PurchaseDate = $_.purchase_date
                CdeContentType = $_.cde_contenttype
                ContentType = $_.content_type
            }
        }
}

# Use kindlegen from Kindle Previewer 3 installation since kindlegen download was discontinued
# TODO: make this configurable
New-Alias kindlegen "$env:LocalAppData\Amazon\Kindle Previewer 3\lib\fc\bin\kindlegen.exe"

function ConvertTo-KindleBook {
    param(
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [ValidateScript({
            if( -Not ($_ | Test-Path) ){
                throw "File or folder does not exist"
            }
            return $true
        })]
        [String] $Path,

        [ValidateSet('AZW3','MOBI')]
        [String] $TargetFormat,

        [String] $Title
    )
    if (-not $Title) {
        $title = RemoveAllExtensions($Path)
    }

    $parentPath = (Split-Path $Path -Parent)
    $cover = (Join-Path $parentPath 'cover.jpg') 
    if (Test-Path $cover) {
        $otherParams = "--cover=`"$cover`""
    }

    Write-Verbose "Using additional parameters: $otherParams"

    if ($TargetFormat -eq 'AZW3') {
        $azw3 = [IO.Path]::ChangeExtension($Path, '.azw3')

        # NOTE: do not use --share-not-sync option as this breaks Vocabulary Builder
        [void] (ebook-convert $Path $azw3 --language=ja --authors=tandoku --title="$title" $otherParams)
        return $azw3
    } else {
        $epub = [IO.Path]::ChangeExtension($Path, '.epub')

        [void] (ebook-convert $Path $epub --epub-version=3 --language=ja --authors=tandoku --title="$title" $otherParams)
        #pandoc $source -f commonmark+footnotes -o $epub -t epub3 --metadata title="$title" --metadata author=Tandoku --metadata lang=ja

        [void] (kindlegen $epub)
        return ([IO.Path]::ChangeExtension($epub, '.mobi'))
    }
}
New-Alias tokindle ConvertTo-KindleBook

function RemoveAllExtensions($path) {
    $result = [IO.Path]::GetFilenameWithoutExtension($path)
    while ([IO.Path]::HasExtension($result)) {
        $result = [IO.Path]::GetFilenameWithoutExtension($result)
    }
    return $result
}

Export-ModuleMember -Function *-* -Alias *
