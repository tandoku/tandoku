using module '../modules/tandoku-yaml.psm1'

param(
    [Parameter()]
    [String[]]
    $Path
)

Get-ChildItem $Path -Filter *.content.yaml |
    ForEach-Object { Import-Yaml $_ } |
    ForEach-Object {
        [PSCustomObject]@{
            ordinal = $_.source.ordinal
            start = $_.source.timecodes.start
            end = $_.source.timecodes.end
            ref_en_ordinal = $_.references.en.source.ordinal
            ref_en_start = $_.references.en.source.timecodes.start
            ref_en_end = $_.references.en.source.timecodes.end
        }
    }
