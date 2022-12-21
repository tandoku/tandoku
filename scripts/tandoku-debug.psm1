function Set-LocationToTandokuDocs {
    $repoRoot = Get-TandokuRepoRoot
    Set-Location (MapToPSDriveAlias $repoRoot/docs)
}
New-Alias tdkdocs Set-LocationToTandokuDocs

function Set-LocationToTandokuScripts {
    $repoRoot = Get-TandokuRepoRoot
    Set-Location (MapToPSDriveAlias $repoRoot/scripts)
}
New-Alias tdkscripts Set-LocationToTandokuScripts

function Set-LocationToTandokuSrc {
    $repoRoot = Get-TandokuRepoRoot
    Set-Location (MapToPSDriveAlias $repoRoot/src)
}
New-Alias tdksrc Set-LocationToTandokuSrc

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

Export-ModuleMember -Function *-* -Alias *
