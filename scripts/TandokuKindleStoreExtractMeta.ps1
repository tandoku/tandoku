param(
    [Parameter(Mandatory=$true)]
    [String]
    $Asin,

    [Parameter()]
    [String]
    $OutFile,

    [Parameter()]
    [String]
    $KindleStoreMetadataPath
)

$cacheFiles = Get-ChildItem $KindleStoreMetadataPath -Filter 'KindleSyncMetadataCache*.xml'

foreach ($cacheFile in $cacheFiles) {
    $xml = [xml] (Get-Content $cacheFile)
    $child = $xml.response.add_update_list.meta_data | Where-Object ASIN -eq $Asin
    if ($child) {
        if ($OutFile) {
            $childXml = [xml] $child.OuterXml
            $writer = [System.IO.File]::CreateText($OutFile)
            $childXml.Save($writer)
            $writer.Close()
        } else {
            $obj = [PSCustomObject] @{
                title = $child.title.'#text'
                titlePronunciation = $child.title.pronunciation
                asin = $child.ASIN
            }
            return $obj
        }
        break
    }
}