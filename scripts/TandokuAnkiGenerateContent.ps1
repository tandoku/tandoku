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

if (-not (Test-Path "$volumePath/content")) {
    [void] (New-Item "$volumePath/content" -ItemType Directory)
}

$cardGroups = $cards |
    ForEach-Object { $i = 0 } { $_; $i++ } |
    Group-Object { [Math]::Floor($i / $MaxBlocksPerFile) + 1 }

$cardGroups |
    Foreach-Object {
        $contentFileNum = $cardGroups.Count -gt 1 ? $_.Name : $null
        $contentPath = "$volumePath/content/content$contentFileNum.yaml"
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
