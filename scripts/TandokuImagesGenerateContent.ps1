param(
    [Parameter()]
    [String]
    $InputPath,

    [Parameter()]
    [String]
    $OutputPath,

    [Parameter()]
    [String]
    $SplitByFileName,

    [Parameter()]
    $Volume
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-volume.psm1" -Scope Local

function GenerateBlocksFromImages {
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [String]
        $ImagePath
    )
    process {
        $source = [IO.FileInfo](Convert-Path $ImagePath)
        $block = [ordered]@{
            image = [ordered]@{
                name = $source.Name
            }
        }

        @{
            FileName = $source.Name
            Block = $block
        }
    }
}

function SaveContentBlocks {
    param(
        [Parameter(Mandatory)]
        [String]
        $Path,

        [Parameter(Mandatory, ValueFromPipelineByPropertyName)]
        [String]
        $Name,

        [Parameter(Mandatory, ValueFromPipelineByPropertyName)]
        $Group
    )

    process {
        $contentPath = "$Path/$Name.content.yaml"
        $Group.Block | ExportYaml $contentPath
        $contentPath
    }
}

$Volume = ResolveVolume $Volume
if (-not $Volume) {
    return
}
$volumePath = $Volume.Path
$volumeSlug = $Volume.Slug

if (-not $InputPath) {
    $InputPath = "$volumePath/images"
}
if (-not $OutputPath) {
    $OutputPath = "$volumePath/content"
}

$imageExtensions = GetImageExtensions
$images = @()
foreach ($imageExtension in $imageExtensions) {
    $images += Get-ChildItem -Path $InputPath -Filter "*$imageExtension"
}

CreateDirectoryIfNotExists $OutputPath

$outputItems = $images |
    WritePipelineProgress -Activity 'Generating content' -ItemName 'image' -TotalCount $images.Count |
    GenerateBlocksFromImages |
    Group-Object {
        if ($SplitByFileName -and $_.FileName -match $SplitByFileName) {
            return $Matches.Count -gt 1 ? $Matches[1] : $Matches[0]
        } else {
            return $volumeSlug
        }
    } |
    SaveContentBlocks $OutputPath

if ($outputItems) {
    TandokuVersionControlAdd -Path $outputItems -Kind text
}
