# TODO - rename this module to tandoku-video.psm1 (or merge to tandoku-media.psm1)

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

function GetSubtitleBaseName($subtitle) {
    # Remove both subtitle and language extension (e.g. .ja.srt)
    return (Split-Path (Split-Path $subtitle -LeafBase) -LeafBase)
}