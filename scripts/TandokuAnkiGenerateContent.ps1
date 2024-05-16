param(
    [Parameter(Mandatory=$true)]
    [String]
    $Path,

    [Parameter()]
    [String]
    $VolumePath,

    [Parameter()]
    [int]
    $MaxBlocksPerFile = 500,

    [Parameter()]
    [Switch]
    $UseReading
)

# TODO - generalize as needed (other Jo-Mako decks - including audio, other Anki decks)

$formatNoteText = {
    param($text)

    $newline = [Environment]::NewLine
    $lines = $text -split '　'
    return ($lines -join "  $newline")
}
$extractImg = {
    param($img)

    if ($img -and $img -match "img src=`"(.+)`"") {
        $imageName = $matches[1]
        return $imageName
    }
}

$columns = @{
    id = @{ Target = 'source.note' }
    img = @{
        Target = 'image.name'
        Extractor = $extractImg
    }
    ref = @{
        Target = 'references.en.text'
        Extractor = $formatNoteText
    }
}
$textColumn = @{
    Target = 'text'
    Extractor = $formatNoteText
}
if ($UseReading) {
    $columns.nativeReading = $textColumn
} else {
    $columns.native = $textColumn
}

$csvParams = @{
    Path = $Path
    VolumePath = $VolumePath
    MaxBlocksPerFile = $MaxBlocksPerFile
    SkipLines = 3
    Delimiter = "`t"
    Header = @('id','native','ref','nativeReading','img')
    BaseFileName = 'notes'
    Columns = $columns
}

TandokuCsvGenerateContent @csvParams