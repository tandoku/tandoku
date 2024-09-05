function ExtractFilmQualifierFromFileName($fileName) {
    return ($fileName -match 's\d{1,4}e\d{1,4}' ? "-$($Matches[0])".ToLowerInvariant() : $null)
}

function GetKnownVideoExtensions([Switch]$FileMask) {
    $prefix = $FileMask ? '*' : ''
    return "$prefix.mkv","$prefix.mp4"
}

function GetKnownSubtitleExtensions([Switch]$FileMask, [String]$Language, [Switch]$MatchLanguagePrefix) {
    $prefix = $FileMask ? '*' : ''
    if ($Language) {
        $prefix = "$prefix.$Language"
        if ($MatchLanguagePrefix) {
            $prefix = "$prefix*"
        }
    }
    return "$prefix.ass","$prefix.srt","$prefix.vtt"
}

function GetVideoForSubtitle($subtitle, $videoDir) {
    # Remove both subtitle and language extension (e.g. .ja.srt)
    $baseName = Split-Path (Split-Path $subtitle -LeafBase) -LeafBase
    return (Get-Item "$videoDir/$baseName.*" -Include (GetKnownVideoExtensions -FileMask))
}