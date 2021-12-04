Import-Module tandoku-utils.psm1

function Process-CalibreChangesAndAddText {
    $calibreBooks = Get-CalibreBook
    $calibreBooks | Update-CalibreMetadataInTandoku

    $tandokuBooks = Get-CalibreBook O:\Tandoku
    $tandokuBooks | Move-TandokuContentByCalibreMetadata

    $calibreBooks | Add-CalibreBookToTandoku
    Get-ChildItem O:\Tandoku\Manga -Filter *.azw3 -Recurse | Unpack-KindleBookImages

    Get-ChildItem O:\Tandoku\Manga\Collection.priority -Filter image*.jpeg -Recurse | Add-GcvText
    Get-ChildItem O:\Tandoku\Manga\Downloads.priority -Filter image*.jpeg -Recurse | Add-GcvText
    Get-ChildItem O:\Tandoku\Manga\Downloads.pri2 -Filter image*.jpeg -Recurse | Add-GcvText
    Get-ChildItem O:\Tandoku\Manga\Downloads.pri3 -Filter image*.jpeg -Recurse | Add-GcvText
    Get-ChildItem O:\Tandoku\Manga\Downloads.pri4 -Filter image*.jpeg -Recurse | Add-GcvText
    Get-ChildItem O:\Tandoku\Manga\Samples -Filter image*.jpeg -Recurse | Add-GcvText
    Get-ChildItem O:\Tandoku\Manga\Samples.priority -Filter image*.jpeg -Recurse | Add-GcvText

    #TODO: clean up empty folders due to moved items
}

#TODO: clean up duplicate books
#unique by ASIN: Get-CalibreBooks|?{$_.ASIN -ne $null}|sort -Property ASIN -Unique|count
#still should get tags and make sure not to delete Sample
#or better, filter to just limited-free books first:
#Get-CalibreBooks|?{$_.Title -match '期間限定'}|sort -Property ASIN -Unique|count

function Get-CalibreBook($RootPath = 'O:\Read\Calibre') {
    $ns = @{
        dc='http://purl.org/dc/elements/1.1/'
        opf='http://www.idpf.org/2007/opf'
    }
    Get-ChildItem -Path $RootPath -Filter metadata.opf -Recurse |
        Foreach-Object {
            $xml = [xml] (Get-Content -LiteralPath "$_")
            $m = $xml.package.metadata
            [PSCustomObject] @{
                Title = $m.title
                Author = $m.creator.'#text'
                Publisher = $m.publisher
                Language = $m.language
                ASIN = (Select-Xml -Xml $m -XPath 'dc:identifier[@opf:scheme="MOBI-ASIN"]' -Namespace $ns).Node.'#text'
                CalibreID = (Select-Xml -Xml $m -XPath 'dc:identifier[@opf:scheme="calibre"]' -Namespace $ns).Node.'#text'
                UUID = (Select-Xml -Xml $m -XPath 'dc:identifier[@opf:scheme="uuid"]' -Namespace $ns).Node.'#text'
                Tags = $m.subject
                MetadataFile = $_
                Location = $_.Directory
            }
        }
}

#Usage: Get-CalibreBook|Update-CalibreMetadataInTandoku
#Ensure that Calibre has written all metadata.opf files first
#Can force this using: calibredb backup_metadata
function Update-CalibreMetadataInTandoku {
    param(
        [Parameter(Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
        [String]
        $UUID,

        [Parameter(Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
        [IO.FileInfo]
        $MetadataFile,

        [Parameter(ValueFromPipelineByPropertyName=$true)]
        [String]
        $Title,

        [Parameter()]
        [String]
        $TargetRoot = 'O:\Tandoku\'
    )
    begin {
        $idMap = @{}
        Get-CalibreBook $TargetRoot |
            Foreach-Object {
                $idMap[$_.UUID] = $_.MetadataFile
            }
    }
    process {
        $target = [IO.FileInfo] $idMap[$UUID]
        if ($target) {
            if (($target.length -ne $MetadataFile.length) -or ($target.lastWriteTime -ne $MetadataFile.lastWriteTime)) {
                $backupPath = "$target.bak"
                if (-not (Test-Path -LiteralPath $backupPath)) {
                    Move-Item -LiteralPath $target -Destination $backupPath
                }
                Copy-Item -LiteralPath $MetadataFile -Destination $target
                $target.Refresh()
                $target
            }
        } else {
            Write-Error -Message "Cannot find metadata under target for $UUID $Title" -TargetObject $MetadataFile
        }
    }
}

#Usage:
# $books = Get-CalibreBooks .
# $books|Move-TandokuContentByCalibreMetadata
# for some reason doing Get-CalibreBooks .|Move-TandokuContentByCalibreMetadata gives an error about path not found but still works
function Move-TandokuContentByCalibreMetadata {
    param(
        [Parameter(Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
        [IO.FileInfo]
        $MetadataFile,

        [Parameter(Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
        [String]
        $Title,

        [Parameter(Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
        [String[]]
        $Author,

        [Parameter(ValueFromPipelineByPropertyName=$true)]
        [String]
        $Publisher,

        [Parameter(ValueFromPipelineByPropertyName=$true)]
        [String[]]
        $Tags,

        [Parameter()]
        [String]
        $TargetRoot = 'O:\Tandoku\'
    )
    process {
        if ($MetadataFile.Directory.Name -ne 'source') {
            Write-Error "Unexpected metadata location: $MetadataFile"
        } else {
            $targetCanonicalRoot = (Convert-Path $TargetRoot)

            $sourcePath = $MetadataFile.Directory.Parent
            $targetPath = [IO.DirectoryInfo] (Get-TandokuPathForCalibreBook -Title $Title -Author $Author -Publisher $Publisher -Tags $Tags -TargetRoot $targetCanonicalRoot)

            if (-not $targetPath) {
                Write-Error "Could not get Tandoku path for $Title with tags: $Tags"
                return
            }

            if ($sourcePath.FullName -ne $targetPath.FullName) {
                if (Test-Path $targetPath) {
                    Write-Error "Target path already exists: $targetPath"
                } else {
                    if (-not (Test-Path $targetPath.Parent)) {
                        New-Item -Type Directory $targetPath.Parent
                    }
                    #Write-Output "Move $sourcePath to $targetPath"
                    Move-Item -LiteralPath $sourcePath -Destination $targetPath
                }
            }
        }
    }
}

function Get-TandokuPathForCalibreBook {
    param(
        [Parameter(Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
        [String]
        $Title,

        [Parameter(Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
        [String[]]
        $Author,

        [Parameter(ValueFromPipelineByPropertyName=$true)]
        [String]
        $Publisher,

        [Parameter(ValueFromPipelineByPropertyName=$true)]
        [String[]]
        $Tags,

        [Parameter()]
        [String]
        $TargetRoot = 'O:\Tandoku\',

        [Parameter()]
        [Switch]
        $AllowUnknown
    )
    process {
        $sourceType = (GetMatchingString $Tags @('Manga', 'Novel', 'Picture Book', 'Reference', 'Film', 'Game'))
        $sourceType = switch($sourceType) {
            'Novel' { 'Novels' }
            'Picture Book' { 'Picture Books' }
            'Film' { 'Films' }
            'Game' { 'Games' }
            default { $sourceType }
        }

        $collection = (GetMatchingString $Tags @('Collection', 'Downloads', 'Sample', 'KCLS'))
        $collection = switch($collection) {
            'Sample' { 'Samples' }
            default { $collection }
        }

        if ($collection) {
            $status = (GetMatchingString $Tags @('Priority', 'Pri[0-9]', 'Rejected', 'Duplicate')).ToLowerInvariant()
            $container = $status ? "$collection.$status" : $collection
        }

        $titleclean = (CleanInvalidPathChars $Title)
        $authorclean = (CleanInvalidPathChars $Author -join ',')
        $pubclean = $Publisher ? (CleanInvalidPathChars $Publisher) : 'None'

        if ($sourceType -and $container) {
            Join-Path $TargetRoot $sourceType $container "$pubclean - $authorclean" $titleclean

            #TODO: remove publisher, author?
            #Join-Path $TargetRoot $sourceType $container $titleclean
        } elseif ($AllowUnknown) {
            Join-Path $TargetRoot 'Unknown' "$pubclean - $authorclean" $titleclean
        } else {
            return $null
        }
        <#
        [PSCustomObject] @{
            TargetRoot = $TargetRoot
            SourceType = $sourceType
            Container = $container
            Publisher = $pubclean
            Author = $authorclean
            Title = $titleclean
        }#>
    }
}

function GetMatchingString([String[]] $array, $find) {
    if (-not $array) {
        return $null
    }
    $findMatch = $find -join '|'
    $pattern = "^($findMatch)$"
    ([string] ($array -match $pattern))
}

function Open-ImagesFromCalibreClipboardMetadata([String] $TandokuRoot = 'O:\Tandoku\') {
    $asin = (Get-Clipboard|select-string '^Identifiers\s*:.*mobi-asin:([^,]+)').matches.groups[1].value
    if (-not ($script:tandokuPathByAsinMap)) {
        $script:tandokuPathByAsinMap = @{}
        Get-CalibreBook $TandokuRoot |
            Foreach-Object {
                $script:tandokuPathByAsinMap[$_.ASIN] = $_.Location.Parent
            }
    }
    $targetPath = $script:tandokuPathByAsinMap[$asin]
    if ($targetPath) {
        start (Join-Path $targetPath 'images')
    } else {
        Write-Error "Tandoku content not found for ASIN $asin"
    }
}

function Add-CalibreBookToTandoku {
    param(
        [Parameter(Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
        [String]
        $Title,

        [Parameter(Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
        [String[]]
        $Author,

        [Parameter(ValueFromPipelineByPropertyName=$true)]
        [String]
        $Publisher,

        [Parameter(ValueFromPipelineByPropertyName=$true)]
        [String[]]
        $Tags,

        [Parameter(Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
        [IO.DirectoryInfo]
        $Location,

        [Parameter()]
        [String]
        $TargetRoot = 'O:\Tandoku\'
    )
    process {
        $targetCanonicalRoot = (Convert-Path $TargetRoot)
        $titlePath = [IO.DirectoryInfo] (Get-TandokuPathForCalibreBook -Title $Title -Author $Author -Publisher $Publisher -Tags $Tags -TargetRoot $targetCanonicalRoot)

        if (-not $titlePath) {
            Write-Error "Could not get Tandoku path for $Title with tags: $Tags"
            return
        }

        $titleclean = $titlePath.Name
        $targetPath = (Join-Path $titlePath 'source')

        if (Test-Path $targetPath) {
            if ((Get-ChildItem -LiteralPath $targetPath -Filter *.azw3).Count -lt 0) {
                Write-Error "Target path already exists with no azw3: $targetPath"
            }
        } else {
            #Write-Output "Copy $Location to $targetPath"
            Copy-Item -LiteralPath $Location -Destination $targetPath -Recurse

            $ebook = (Get-ChildItem -LiteralPath $targetPath -Filter *.azw3)[0]
            $ebookTarget = (Join-Path $targetPath "$titleclean.azw3")
            Move-Item -LiteralPath $ebook -Destination $ebookTarget
            [IO.FileInfo] $ebookTarget
        }
    }
}

#TODO: books with multiple authors
#Get-CalibreBooks|? language -eq 'jpn'|Select-Object Title,Author,Publisher,Edition,Source,@{Name="AuthorType";Expression={($_.Author.GetType().Name)}}|? AuthorType -ne 'String'|Select-Object Title,Publisher,Edition,Source,@{Name="Author";Expression={([String]::Join(',', $_.Author))}}
#TODO: books with no publisher
#Get-CalibreBooks|? language -eq 'jpn'|? publisher -eq $null

function Unpack-KindleBookImages {
    param(
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [ValidateScript({
            if (-not (Test-Path -LiteralPath $_)) {
                throw "File does not exist: $_"
            }
            return $true
        })]
        [String]
        $Path,

        [Parameter()]
        [Switch]
        $KeepUnpackDirectory
    )
    process {
        $source = [IO.FileInfo](Convert-Path -LiteralPath $Path)
        $imagesTarget = (Join-Path $source.Directory.Parent 'images')
        if (Test-Path $imagesTarget) {
            if ((Get-ChildItem $imagesTarget -Filter *.jp*g).Count -lt 0) {
                Write-Error "$imagesTarget already exists but with no images"
            }
        } else {
            $unpackTo = (Join-Path $source.Directory 'unpack')
            if (-not (Test-Path $unpackTo)) {
                Write-Verbose "Unpacking $source to $unpackTo"
#TODO: would like to redirect output to only write when verbose, but *>$null or 1>$null doesn't work
                kindleunpack -i $source $unpackTo
            } else {
                Write-Verbose "$unpackTo already exists, using existing content"
            }

            $imagesSource = [IO.DirectoryInfo](Join-Path $unpackTo 'mobi7\images')
            if (Test-Path $imagesSource) {
                Write-Verbose "Copying images to $imagesTarget"
                Copy-Item -LiteralPath $imagesSource -Destination $imagesTarget -Recurse
            } else {
                Write-Error "$imagesSource not found, skipping"
            }

            if (-not $KeepUnpackDirectory) {
                Remove-ItemTree $unpackTo
            }
        }
    }
}

function kindleunpack {
    $pypath = (Convert-Path O:\Tandoku\Tools\KindleUnpack\lib\kindleunpack.py)
    python $pypath $args
}

function Remove-ItemTree($path) {
    Get-ChildItem -LiteralPath $path -Recurse |  #Find all children
        Select-Object FullName,@{Name='PathLength';Expression={($_.FullName.Length)}} |  #Calculate the length of their path
        Sort-Object PathLength -Descending | #sort by path length descending
        %{ Get-Item -LiteralPath $_.FullName } | 
        Remove-Item -Force
    Remove-Item -LiteralPath $path -Recurse -Force
}

function Search-CalibreBooks($s) {
    gci O:\Tandoku\Scripts\booklist.txt|sls -AllMatches $s
}

function Save-CalibreBookList {
    Get-CalibreBooks |
        Sort-STNumerical -MaximumDigitCount 4 |
        Out-File booklist.txt
}

Export-ModuleMember -Function *-*
