param(
    [Parameter()]
    [String]
    $SplitByFileName,

    # TODO - $MaxBlocksPerFile

    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

function GenerateBlocksFromAcvText {
    param(
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [String]
        $ImagePath
    )
    process {
        # TODO - should really share this with AddAcvText in TandokuImagesAnalyze.ps1
        $source = [IO.FileInfo](Convert-Path $ImagePath)
        $textDir = (Join-Path $source.Directory 'text')
        $acvPath = (Join-Path $textDir "$([IO.Path]::GetFilenameWithoutExtension($source.Name)).acv4.json")
        if (Test-Path $acvPath) {
            $acv = Get-Content $acvPath | ConvertFrom-Json

            $rootBlock = @{
                image = @{
                    name = $source.Name
                }
                blocks = @($acv.readResult.blocks.lines | ForEach-Object {
                    return @{ text = $_.text }
                })
            }

            if ($rootBlock.blocks.Count -gt 0) {
                if ($rootBlock.blocks.Count -eq 1) {
                    $block = $rootBlock.blocks[0]
                    $rootBlock.Remove('blocks')
                    $rootBlock += $block
                }

                $result = @{
                    FileName = $source.Name
                    Block = $rootBlock
                }
                Write-Output $result
            }
        } else {
            Write-Warning "Skipping $ImagePath because $acvPath is missing"
        }
    }
}

# TODO - consider factoring this out and reusing in TandokuCsvGenerateContent
function SaveContentBlocks {
    param(
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
            $contentBaseName = $Matches.Count -gt 0 ? $Matches[1] : $Matches[0]
        } else {
            $contentBaseName = ''
        }

        if ($contentBaseName -ne $currentContentBaseName) {
            if ($writer) {
                $writer.Close()
                Write-Output $contentPath
            }
            $currentContentBaseName = $contentBaseName
            $contentFileName = $contentBaseName ?
                "$contentBaseName.content.yaml" :
                'content.yaml'
            $contentPath = "$volumePath/content/$contentFileName"
            CreateDirectoryIfNotExists "$volumePath/content"
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

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

$path = "$VolumePath/images"
$imageExtensions = GetImageExtensions

$images = @()
foreach ($imageExtension in $imageExtensions) {
    $images += Get-ChildItem -Path $path -Filter "*$imageExtension"
}

$outputItems = $images |
    WritePipelineProgress -Activity 'Generating content' -ItemName 'image' -TotalCount $images.Count |
    GenerateBlocksFromAcvText |
    SaveContentBlocks

if ($outputItems) {
    TandokuVersionControlAdd -Path $outputItems -Kind text
}