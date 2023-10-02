param(
    [Parameter(Mandatory=$true)]
    [String]
    $Path,

    [Parameter()]
    [String]
    $VolumePath
)

# Collect .mp4/.mkv files at $Path (tandoku source import)
# Find corresponding subtitles (tandoku source import)
# Note: import reference language subtitles into /source/ref-en/ path