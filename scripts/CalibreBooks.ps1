function Process-CalibreChangesAndAddOcr {
    Get-CalibreBook | Update-CalibreMetadataInTandoku
    $books = Get-CalibreBook O:\Tandoku
    $books | Move-TandokuContentByCalibreMetadata

    Get-ChildItem O:\Tandoku\Manga\Collection.priority -Filter image*.jpeg -Recurse | Add-GcvOcr
    Get-ChildItem O:\Tandoku\Manga\Downloads.priority -Filter image*.jpeg -Recurse | Add-GcvOcr
    Get-ChildItem O:\Tandoku\Manga\Samples.priority -Filter image*.jpeg -Recurse | Add-GcvOcr

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
        if ($target -ne $null) {
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
            Write-Error -Message "Cannot find metadata under target for $UUID" -TargetObject $MetadataFile
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
        $TargetRoot = 'O:\Tandoku\'
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
            $status = (GetMatchingString $Tags @('Priority', 'Rejected', 'Duplicate')).ToLowerInvariant()
            $container = $status ? "$collection.$status" : $collection
        }

        $titleclean = (CleanInvalidPathChars $Title)
        $authorclean = (CleanInvalidPathChars $Author -join ',')
        $pubclean = $Publisher ? (CleanInvalidPathChars $Publisher) : 'None'

        if ($sourceType -and $container) {
            Join-Path $TargetRoot $sourceType $container "$pubclean - $authorclean" $titleclean
        } else {
            Join-Path $TargetRoot 'Unknown' "$pubclean - $authorclean" $titleclean
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

function CleanInvalidPathChars($name, $replaceWith = '_') {
    ($name.Split([IO.Path]::GetInvalidFileNameChars()) -join $replaceWith).Trim()
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

#TODO: clean this up to use Get-TandokuPathForCalibreBook (use title for azw3 from path)
function Copy-CalibreBookToTandoku {
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
        [String]
        $Edition,

        [Parameter(Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
        [IO.DirectoryInfo]
        $Location,

        [Parameter()]
        [String]
        $TargetRoot = 'O:\Tandoku\Manga\'
    )
    process {
        $kind = switch($Edition) {
            'Sample' { 'Samples' }
            'Time-limited' { 'Downloads' }
            default { 'Collection' }
        }
        $pubclean = switch($Publisher) {
            $null { 'None' }
            '' { 'None' }
            default { $Publisher }
        }
        $authorclean = [String]::Join(',', $Author)
        #TODO: $titleclean = replace any [IO.Path]::GetInvalidFilenameChars() with _ (or just remove)
        $target = (Join-Path $TargetRoot $kind "$pubclean - $authorclean" $Title 'source')
        if (Test-Path $target) {
            Write-Error "$target already exists, skipping"
        } else {
            Copy-Item -LiteralPath $Location -Destination $target -Recurse

            $ebook = (Get-ChildItem -LiteralPath $target -Filter *.azw3)[0]
            Move-Item -LiteralPath $ebook -Destination "$target\$Title.azw3"
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
            if( -Not ($_ | Test-Path) ){
                throw "File or folder does not exist"
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
        $source = [IO.FileInfo](Convert-Path $Path)
        $imagesTarget = (Join-Path $source.Directory.Parent 'images')
        if (Test-Path $imagesTarget) {
            Write-Error "$imagesTarget already exists, skipping"
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

# Note: I added the call to .Normalize([Text.NormalizationForm]::FormKC) in order to handle full-width numbers
# (which Convert.ToInt32 doesn't handle)
# adapted from https://www.powershelladmin.com/wiki/Sort_strings_with_numbers_more_humanely_in_PowerShell
function Sort-STNumerical {
    <#
        .SYNOPSIS
            Sort a collection of strings containing numbers, or a mix of this and 
            numerical data types - in a human-friendly way.

            This will sort "anything" you throw at it correctly.

            Author: Joakim Borger Svendsen, Copyright 2019-present, Svendsen Tech.

            MIT License

        .PARAMETER InputObject
            Collection to sort.

        .PARAMETER MaximumDigitCount
            Maximum numbers of digits to account for in a row, in order for them to be sorted
            correctly. Default: 100. This is the .NET framework maximum as of 2019-05-09.
            For IPv4 addresses "3" is sufficient, but "overdoing" does no or little harm. It might
            eat some more resources, which can matter on really huge files/data sets.

        .EXAMPLE
            $Strings | Sort-STNumerical

            Sort strings containing numbers in a way that magically makes them sorted human-friendly
            
        .EXAMPLE
            $Result = Sort-STNumerical -InputObject $Numbers
            $Result

            Sort numbers in a human-friendly way.
    #>
    [CmdletBinding()]
    Param(
        [Parameter(
            Mandatory = $True,
            ValueFromPipeline = $True,
            ValueFromPipelineBypropertyName = $True)]
        [System.Object[]]
        $InputObject,
        
        [ValidateRange(2, 100)]
        [Byte]
        $MaximumDigitCount = 10)
    
    Begin {
        [System.Object[]] $InnerInputObject = @()
    }
    
    Process {
        $InnerInputObject += $InputObject
    }

    End {
        $InnerInputObject |
            Sort-Object -Property `
                @{ Expression = {
                    [Regex]::Replace($_, '(\d+)', {
                        "{0:D$MaximumDigitCount}" -f [Int] $Args[0].Value.Normalize([Text.NormalizationForm]::FormKC) })
                    }
                },
                @{ Expression = { $_ } }
    }
}

