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
Set-Alias kindlegen "$env:LocalAppData\Amazon\Kindle Previewer 3\lib\fc\bin\kindlegen.exe"

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
        ebook-convert $Path $azw3 --language=ja --authors=tandoku --title="$title" $otherParams
    } else {
        $epub = [IO.Path]::ChangeExtension($Path, '.epub')

        ebook-convert $Path $epub --epub-version=3 --language=ja --authors=tandoku --title="$title" $otherParams
        #pandoc $source -f commonmark+footnotes -o $epub -t epub3 --metadata title="$title" --metadata author=Tandoku --metadata lang=ja

        kindlegen $epub
    }
}
Set-Alias tokindle ConvertTo-KindleBook

function RemoveAllExtensions($path) {
    $result = [IO.Path]::GetFilenameWithoutExtension($path)
    while ([IO.Path]::HasExtension($result)) {
        $result = [IO.Path]::GetFilenameWithoutExtension($result)
    }
    return $result
}

Export-ModuleMember -Function *-* -Alias *
