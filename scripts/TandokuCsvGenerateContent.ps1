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
    # - Target: section|text|actor|image.name|reference.en.text|reference.en.actor|source.resource
    # - Order: int
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
    Sort-Object {$Columns[$_].Order} |
    ForEach-Object {
        $def = $Columns[$_]
        return [PSCustomObject]@{
            Name = $_
            Target = $def.Target
            Order = $def.Order
            ContentKind = $def.ContentKind
            Extractor = $def.Extractor
        }
    }

$sectionColumn = $orderedColumns |
    Where-Object Target -eq 'section' |
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
                $block = @{}
                foreach ($column in $orderedColumns) {
                    if ($column.Target -eq 'section') {
                        # Handled above already
                        continue
                    }

                    $value = ExtractValue $_ $column
                    if (-not $value) {
                        continue
                    }

                    if ($column.ContentKind) {
                        $block.contentKind = $column.ContentKind
                    }

                    # TODO - factor this out to a function (set value on object via dotted-path)
                    $targetPath = $column.Target -split '\.'
                    $target = $block
                    for ($i = 0; $i -lt $targetPath.Count; $i++) {
                        $prop = $targetPath[$i]
                        if ($i -lt $targetPath.Count - 1) {
                            if (-not $target[$prop]) {
                                $target[$prop] = @{}
                            }
                            $target = $target[$prop]
                        } else {
                            # TODO - handle composite blocks (value is already set)
                            $target[$prop] = $value
                        }
                    }
                }
                (ConvertTo-Yaml $block).TrimEnd()
                '---'
            } |
            Set-Content $contentPath

        TandokuVersionControlAdd -Path $contentPath -Kind text
    }
