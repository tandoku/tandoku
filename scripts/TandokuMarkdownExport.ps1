param(
    [Parameter()]
    [String]
    $VolumePath,

    [Parameter()]
    [Switch]
    $NoFootnotes
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

function GenerateMarkdown($contentPath, $targetDirectory) {
    $content = Get-Content $contentPath | ConvertFrom-Yaml -AllDocuments
    $footnote = 0
    foreach ($block in $content) {
        # Heading
        $heading = $block.source.block
        if ($heading) {
            Write-Output "# $heading"
            Write-Output ''
        }

        # Image
        $imageName = $block.image.name
        if ($imageName) {
            $imagePath = Join-Path $volumePath "images/$imageName"
            $imageRelativePath = [IO.Path]::GetRelativePath($targetDirectory, $imagePath)
            # TODO - factor this out (try using Uri class)
            $imageUrl = $imageRelativePath.Replace('\', '/').Replace('(', '%28').Replace(')', '%29').Replace(' ', '%20')
            Write-Output "![$heading]($imageUrl)"
            Write-Output ''
        }

        # Text
        $blockText = $block.text
        $blockRefText = $block.references.en # TODO - should be references.en.text
        if ($NoFootnotes) {
            Write-Output $blockText
            Write-Output ''
            Write-Output $blockRefText # TODO - add show/hide css
            Write-Output ''
        } else {
            $footnote += 1
            Write-Output "$blockText [^$footnote]"
            Write-Output ''
            Write-Output "[^$footnote]: $blockRefText"
            Write-Output ''
        }
    }
}

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

# TODO - add this as another property on volume info
# also consider dropping the moniker from this (just the cleaned title)
# Note - this is only needed when producing single merged .md file
$volumeBaseFileName = Split-Path $volumePath -Leaf

# TODO - use 'markdown' directory instead and subdirectories for format options
# e.g. markdown/[split-][inlineref]
#   or markdown/[nomerge-][nofootnotes]
$targetDirectory = Join-Path $volumePath 'export'
CreateDirectoryIfNotExists $targetDirectory
$targetPath = Join-Path $targetDirectory "$volumeBaseFileName.md"

$contentFiles =
    @(Get-ChildItem "$volumePath/content" -Filter content.yaml) +
    @(Get-ChildItem "$volumePath/content" -Filter *.content.yaml)

# TODO - Split/NoMerge option to generate .md file for each content file
$contentFiles |
    Foreach-Object { GenerateMarkdown $_ $targetDirectory } |
    Set-Content $targetPath