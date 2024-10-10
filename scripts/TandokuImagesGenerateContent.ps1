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
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
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

# TODO - consider factoring this out and reusing in TandokuCsvGenerateContent
function SaveContentBlocks {
    param(
        [Parameter(Mandatory=$true)]
        $Path,

        [Parameter(Mandatory=$true)]
        [String]
        $VolumeSlug,

        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        $BlockResult
    )
    begin {
        $currentContentBaseName = $null
    }
    process {
        $fileName = $BlockResult.FileName
        $block = $BlockResult.Block

        if ($SplitByFileName -and $fileName -match $SplitByFileName) {
            $contentBaseName = $Matches.Count -gt 1 ? $Matches[1] : $Matches[0]
        } else {
            $contentBaseName = $VolumeSlug
        }

        if ($contentBaseName -ne $currentContentBaseName) {
            if ($writer) {
                $writer.Close()
                Write-Output $contentPath
            }
            $currentContentBaseName = $contentBaseName
            $contentFileName = "$contentBaseName.content.yaml"
            $contentPath = "$Path/$contentFileName"
            CreateDirectoryIfNotExists $Path
            $writer = [IO.File]::CreateText($contentPath)
        }

        $writer.WriteLine((ConvertTo-Yaml $block).TrimEnd())
        $writer.WriteLine('---')
    }
    end {
        if ($writer) {
            $writer.Close()
            Write-Output $contentPath
        }
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

$outputItems = $images |
    WritePipelineProgress -Activity 'Generating content' -ItemName 'image' -TotalCount $images.Count |
    GenerateBlocksFromImages |
    SaveContentBlocks $OutputPath $volumeSlug

if ($outputItems) {
    TandokuVersionControlAdd -Path $outputItems -Kind text
}
