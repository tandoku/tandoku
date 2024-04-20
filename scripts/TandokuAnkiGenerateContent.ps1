param(
    [Parameter(Mandatory=$true)]
    [String]
    $Path,

    [Parameter()]
    [String]
    $VolumePath,

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

$contentPath = "$volumePath/content/content.yaml"
$cards |
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