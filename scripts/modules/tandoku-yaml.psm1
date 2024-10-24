using module './tandoku-utils.psm1'

function Import-Yaml($LiteralPath) {
    if (-not (Test-Path -LiteralPath $LiteralPath)) {
        throw "File not found: $LiteralPath"
    }

    if (TestCommand yq) {
        # Use yq to convert YAML to JSON as ConvertFrom-Json is much faster
        # than ConvertFrom-Yaml for large YAML documents/streams
        # Note that yq needs Console set to UTF-8 encoding (same as tandoku CLI)
        return (yq -o=json -I=0 '.' $LiteralPath | ConvertFrom-Json -AsHashtable)
    } else {
        Write-Warning 'Using ConvertFrom-Yaml because yq is not available (this can be extremely slow for large files)'
        return (Get-Content -LiteralPath $LiteralPath | ConvertFrom-Yaml -AllDocuments -Ordered)
    }
}

function Export-Yaml {
    param(
        [Parameter(Mandatory)]
        [String]
        $Path,

        [Parameter(Mandatory, ValueFromPipeline)]
        $InputObject
    )

    begin {
        SetCurrentDirectory
        $writer = [IO.File]::CreateText($Path)
        $first = $true
    }
    process {
        if ($first) {
            $first = $false
        } else {
            $writer.WriteLine('---')
        }
        $writer.WriteLine((ConvertTo-Yaml $InputObject).TrimEnd())
    }
    end {
        $writer.Close()
    }
}