param(
    [Parameter()]
    $Path = '.'
)

# TODO: use $Path directly if it is a file, or check for metadata.opf child item if it is a directory

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