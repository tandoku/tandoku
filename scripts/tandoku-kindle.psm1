# cp 'e:\documents\My Clippings.txt' o:\Tandoku\Kindle\raw
# cp 'e:\system\vocabulary\vocab.db' o:\Tandoku\Kindle\raw

function Copy-KindleMetadataCache {
    $dt = [DateTime]::Today.ToString('yyyy-MM-dd')
    Copy-Item "$env:LOCALAPPDATA\Amazon\Kindle\Cache\KindleSyncMetadataCache.xml" "KindleSyncMetadataCache-$dt.xml"
}

function Get-KindleBooks {
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
