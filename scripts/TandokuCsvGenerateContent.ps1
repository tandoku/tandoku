param(
    [Parameter(Mandatory=$true)]
    [String]
    $Path,

    [Parameter()]
    [String]
    $VolumePath,

    [Parameter()]
    [int]
    $MaxBlocksPerFile = 500,

    [Parameter()]
    [int] # TODO - allow string (prefix to skip, e.g. '#')
    $SkipLines,

    [Parameter()]
    [char]
    $Delimiter,

    [Parameter()]
    [String[]]
    $Header,

    [Parameter()]
    [String]
    $BaseFileName,

    # Columns: hashtable of column name to objects with these properties:
    # - Target: #section|text|actor|image.name|reference.en.text|reference.en.actor|source.resource
    # - BlockOrder: int
    # - ContentKind: <see schemas/content.yaml>
    # - Extractor: scriptblock
    [Parameter()]
    [Hashtable]
    $Columns
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

function ExtractValue($obj, $column) {
    $colName = $column.Name
    $value = $obj.$colName
    if ($value -and $column.Extractor) {
        return & $column.Extractor $value
    } else {
        return $value
    }
}

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

$lines = Get-Content $Path
if ($SkipLines) {
    $lines = $lines | Select-Object -Skip $SkipLines
}

$csvParams = @{}
if ($Delimiter) {
    $csvParams.Delimiter = $Delimiter
}
if ($Header) {
    $csvParams.Header = $Header
}
$data = $lines | ConvertFrom-Csv @csvParams

CreateDirectoryIfNotExists "$volumePath/content"

$orderedColumns = $Columns.Keys |
    Sort-Object {$Columns[$_].BlockOrder} |
    ForEach-Object {
        $def = $Columns[$_]
        return [PSCustomObject]@{
            Name = $_
            Target = $def.Target
            BlockOrder = $def.BlockOrder
            ContentKind = $def.ContentKind
            Extractor = $def.Extractor
        }
    }

$sectionColumn = $orderedColumns |
    Where-Object Target -eq '#section' |
    Select-Object -First 1

$dataGroups = $data |
    ForEach-Object { $i = 0; $section = $null } {
        if ($sectionColumn) {
            $nextSection = ExtractValue $_ $sectionColumn
            if ($nextSection -ne $section) {
                $i = 0
                $section = $nextSection
            }
        }
        $_
        $i++
    } |
    Group-Object {
        $groupNum = [Math]::Floor($i / $MaxBlocksPerFile) + 1
        if ($section) {
            return ($groupNum -gt 1 ? "$section$groupNum" : $section)
        } else {
            return $groupNum
        }
    }

$dataGroups |
    Foreach-Object {
        $contentFileName = ($sectionColumn -or $dataGroups.Count -gt 1) ?
            "$BaseFileName$($_.Name).content.yaml" :
            'content.yaml'
        $contentPath = "$volumePath/content/$contentFileName"

        $_.Group |
            Foreach-Object {
                $rootBlock = @{}
                $block = $null
                $blockNum = $null
                foreach ($column in $orderedColumns) {
                    $target = $column.Target
                    if (-not $target -or $target -eq '#section') {
                        # Ignore columns without a target; section is handled above already
                        continue
                    }

                    $value = ExtractValue $_ $column
                    if (-not $value) {
                        continue
                    }

                    if ($column.BlockOrder) {
                        if ($column.BlockOrder -ne $blockNum) {
                            $block = @{}
                            $blockNum = $column.BlockOrder
                            if (-not $rootBlock.blocks) {
                                $rootBlock.blocks = @()
                            }
                            $rootBlock.blocks += $block
                        }
                        $currentBlock = $block
                    } else {
                        $currentBlock = $rootBlock
                    }

                    if ($column.ContentKind) {
                        $currentBlock.contentKind = $column.ContentKind
                    }

                    SetValueByPath $currentBlock $target $value
                }

                # TODO - share this with TandokuImagesGenerateContent
                if ($rootBlock.blocks.Count -eq 1) {
                    $block = $rootBlock.blocks[0]
                    $rootBlock.Remove('blocks')
                    $rootBlock += $block
                }

                (ConvertTo-Yaml $rootBlock).TrimEnd()
                '---'
            } |
            Set-Content $contentPath

        TandokuVersionControlAdd -Path $contentPath -Kind text
    }
