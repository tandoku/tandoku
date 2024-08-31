function ExtractFilmQualifierFromFileName($fileName) {
    return ($fileName -match 's\d{1,4}e\d{1,4}' ? "-$($Matches[0])".ToLowerInvariant() : $null)
}

function GetKnownVideoExtensions([Switch]$FileMask) {
    $prefix = $FileMask ? '*' : ''
    return "$prefix.mkv","$prefix.mp4"
}