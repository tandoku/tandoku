param(
    [Parameter()]
    $Path = '.'
)

$opfPath = (Get-Item "$Path/metadata.opf")
if ($opfPath) {
    $xml = [xml] (Get-Content $opfPath)
    $obj = [PSCustomObject] @{
        title = $xml.package.metadata.title
        asin = ($xml.package.metadata.identifier | Where-Object scheme -eq 'MOBI-ASIN').'#text'
    }
    return $obj
} else {
    Write-Error "No metadata.opf file at specified path $Path"
}