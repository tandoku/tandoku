param(
    # TODO: add -Path argument that can be specified instead of -Asin
    # (support either KindleSyncMetadataCache.xml or single kindle-metadata.xml?)

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

# TODO: read $KindleStoreMetadataPath from ~/.tandoku/config.yaml if not specified

$cacheFiles = Get-ChildItem $KindleStoreMetadataPath -Filter 'KindleSyncMetadataCache*.xml'

$found = $false
foreach ($cacheFile in $cacheFiles) {
    $xml = [xml] (Get-Content $cacheFile)
    $child = $xml.response.add_update_list.meta_data | Where-Object ASIN -eq $Asin
    if ($child) {
        $found = $true
        if ($OutFile) {
            # Write formatted XML to file with XML declaration
            # (only XmlDocument.Save(TextWriter) overload writes the XML declaration automatically)
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

if (-not $found) {
    Write-Warning "Could not find ASIN $Asin in $KindleStoreMetadataPath"
}