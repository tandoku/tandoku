param(
    [Parameter(Mandatory=$true)]
    [String]
    $Path,

    [Parameter()]
    [String]
    $Moniker,

    [Parameter()]
    [String[]]
    $Tags
)

# Collect .mp4/.mkv files at $Path (tandoku source import)
# Find corresponding subtitles (tandoku source import)