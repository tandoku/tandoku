param(
    [Parameter()]
    [ValidateSet('anime', 'live-action', 'novel', 'visual-novel', 'web-novel')]
    [String[]]
    $ContentType
)

foreach ($type in $ContentType) {
    .\Get-JpdbDifficultyList.ps1 -ContentType $type | Export-Csv "$type-difficulty-list.csv"
}
