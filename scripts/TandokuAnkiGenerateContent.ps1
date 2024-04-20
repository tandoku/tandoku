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
    [Switch]
    $UseReading
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

function FormatCardText($text) {
    $newline = [Environment]::NewLine
    $lines = $text -split '　'
    return ($lines -join "  $newline")
}

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

# TODO - generalize as needed (other Jo-Mako decks - including audio, other Anki decks)
$cards = Get-Content $path |
    Select-Object -Skip 3 |
    ConvertFrom-Csv -Delimiter `t -Header @('id','native','ref','nativeReading','img')

CreateDirectoryIfNotExists "$volumePath/content"

$cardGroups = $cards |
    ForEach-Object { $i = 0 } { $_; $i++ } |
    Group-Object { [Math]::Floor($i / $MaxBlocksPerFile) + 1 }

$cardGroups |
    Foreach-Object {
        $contentFileName = $cardGroups.Count -gt 1 ?
            "cards$($_.Name).content.yaml" :
            'content.yaml'
        $contentPath = "$volumePath/content/$contentFileName"

        $_.Group |
            Foreach-Object {
                $block = @{
                    text = FormatCardText ($UseReading ? $_.nativeReading : $_.native)
                    source = @{
                        block = $_.id
                    }
                }
                if ($_.img -and $_.img -match "img src=`"(.+)`"") {
                    $imageName = $matches[1]
                    $block.image = @{
                        name = $imageName
                    }
                }
                if ($_.ref) {
                    $block.references = @{
                        en = @{
                            text = FormatCardText $_.ref
                        }
                    }
                }
                (ConvertTo-Yaml $block).TrimEnd()
                '---'
            } |
            Set-Content $contentPath

        TandokuVersionControlAdd -Path $contentPath -Kind text
    }
