param(
    [Parameter(Mandatory=$true)]
    [String]
    $Path
)

function GetElementText($el) {
    return $el -is [System.Xml.XmlElement] ? $el.'#text' : [string]$el
}

$xml = [xml] (Get-Content $Path)
if ($xml) {
    $obj = [PSCustomObject] @{
        # TODO: extract additional metadata (ISBN, creator, publisher, file-as for each, language, book-type, primary-writing-mode, original-resolution)
        title = GetElementText $xml.package.metadata.title
        asin = GetElementText ($xml.package.metadata.identifier | Where-Object scheme -eq 'MOBI-ASIN')
    }
    return $obj
}