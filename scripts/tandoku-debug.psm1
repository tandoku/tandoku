# NOTE: The output of these commands is functionally equivalent to the Markdown output currently

function Get-AllTextFromTandokuContent {
    param(
        [Parameter()]
        [String]
        $Path
    )

    $stream = Get-Content $Path|ConvertFrom-Yaml -AllDocuments
    foreach ($block in $stream) {
        $block.Location
        $block.Text
    }
}

function Save-AllTextFromTandokuContent {
    param(
        [Parameter()]
        [String]
        $Path
    )

    $outPath = [IO.Path]::ChangeExtension($Path, '.txt')
    Get-AllTextFromTandokuContent $Path | Out-File $outPath
}
