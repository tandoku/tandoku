[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Character,

    [Parameter(Mandatory)]
    [ValidateSet('uchisen')]
    [string]$Source,

    [string]$Path
)

$ErrorActionPreference = 'Stop'

# Load prime Unicode characters from uchisen-primes.yaml
$script:primesFile = Join-Path $PSScriptRoot 'uchisen-primes.yaml'
$script:primeChars = [ordered]@{}
if (Test-Path $script:primesFile) {
    foreach ($line in [System.IO.File]::ReadAllLines($script:primesFile, [System.Text.UTF8Encoding]::new($false))) {
        if ($line -match '^([^#:]+):\s*(.*)$') {
            $script:primeChars[$Matches[1].Trim()] = $Matches[2].Trim()
        }
    }
}

function Get-NodeId {
    param([string]$Name)
    $first = ($Name -split ',')[0].Trim()
    return ($first -replace ' ', '_')
}

function Get-UchisenDecomposition {
    param([string]$KanjiChar)

    $encodedChar = [Uri]::EscapeDataString($KanjiChar)
    $url = "https://uchisen.com/kanji/$encodedChar"

    # Return existing node ID if already processed
    foreach ($n in $script:nodes.Values) {
        if ($n.Url -eq $url) { return $n.NodeId }
    }

    Write-Verbose "Fetching decomposition for $KanjiChar from $url"
    $response = Invoke-WebRequest -Uri $url -UseBasicParsing
    $html = $response.Content

    # Extract kanji name (e.g., "知 - Know" or "口 - Mouth, Entrance")
    if ($html -notmatch '<div class="kanji_name">\s*<span>(.+?)\s+-\s+(.+?)</span>') {
        throw "Could not extract name for character '$KanjiChar' from $url"
    }
    $name = $Matches[2].Trim()
    $nodeId = Get-NodeId $name

    $children = [System.Collections.Generic.List[string]]::new()

    # Parse the components section
    $compIdx = $html.IndexOf('class="components">')
    if ($compIdx -ge 0) {
        # Find the end of the components section
        $endIdx = $html.IndexOf('class="vocab_div"', $compIdx)
        if ($endIdx -lt 0) { $endIdx = $html.IndexOf('class="queue_container"', $compIdx) }
        if ($endIdx -lt 0) { $endIdx = $html.IndexOf("class='sub_header'", $compIdx) }
        if ($endIdx -lt 0) { $endIdx = $html.IndexOf('class="sub_header"', $compIdx) }
        if ($endIdx -lt 0) { $endIdx = [Math]::Min($compIdx + 5000, $html.Length) }
        $compHtml = $html.Substring($compIdx, $endIdx - $compIdx)

        # Extract primes (listed before compound kanji in the HTML)
        foreach ($m in [regex]::Matches($compHtml, 'href="/primes/([^"]+)"[^>]*>([^:]+):')) {
            $primePath = $m.Groups[1].Value
            $primeName = $m.Groups[2].Value.Trim()
            $primeId = Get-NodeId $primeName

            if (-not $script:nodes.ContainsKey($primeId)) {
                $primeChar = ''
                if ($script:primeChars.Contains($primeName)) {
                    $primeChar = $script:primeChars[$primeName]
                } else {
                    $script:primeChars[$primeName] = ''
                }
                $script:nodes[$primeId] = @{
                    NodeId    = $primeId
                    Character = $primeChar
                    Name      = $primeName
                    Type      = 'prime'
                    Url       = "https://uchisen.com/primes/$primePath"
                    Children  = [System.Collections.Generic.List[string]]::new()
                }
            }
            $children.Add($primeId)
        }

        # Extract compound kanji components
        foreach ($m in [regex]::Matches($compHtml, 'href="/kanji/[^"]*"[^>]*>([^:]+):\s*(?:&nbsp;)?\s*<span class="component_symbol">([^<]+)</span>')) {
            $childChar = $m.Groups[2].Value.Trim()
            $childId = Get-UchisenDecomposition -KanjiChar $childChar
            $children.Add($childId)
        }
    }

    $script:nodes[$nodeId] = @{
        NodeId    = $nodeId
        Character = $KanjiChar
        Name      = $name
        Type      = 'kanji'
        Url       = $url
        Children  = $children
    }

    return $nodeId
}

# Filter to kanji characters only (CJK Unified Ideographs + Extension A)
$kanjiChars = $Character.ToCharArray() | Where-Object {
    $code = [int]$_
    ($code -ge 0x4E00 -and $code -le 0x9FFF) -or ($code -ge 0x3400 -and $code -le 0x4DBF)
}

if (-not $kanjiChars) {
    throw "No kanji characters found in input '$Character'"
}

if ($Path) {
    $resolvedPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    # For .mermaid file mode with multiple characters, clear the file first
    if ($resolvedPath -like '*.mermaid' -and $kanjiChars.Count -gt 1 -and (Test-Path $resolvedPath)) {
        Remove-Item $resolvedPath
    }
}

$isFirst = $true
foreach ($kanjiChar in $kanjiChars) {
    $script:nodes = [System.Collections.Hashtable]::new([System.StringComparer]::Ordinal)

    # Perform recursive decomposition
    $rootId = Get-UchisenDecomposition -KanjiChar $kanjiChar

    # BFS traversal for output ordering
    $ordered = [System.Collections.Generic.List[string]]::new()
    $queue = [System.Collections.Queue]::new()
    $visited = [System.Collections.Hashtable]::new([System.StringComparer]::Ordinal)
    $queue.Enqueue($rootId)
    $visited[$rootId] = $true

    while ($queue.Count -gt 0) {
        $current = $queue.Dequeue()
        $ordered.Add($current)
        foreach ($child in $script:nodes[$current].Children) {
            if (-not $visited.ContainsKey($child)) {
                $visited[$child] = $true
                $queue.Enqueue($child)
            }
        }
    }

    # Build Mermaid graph
    $rootNode = $script:nodes[$rootId]
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("---")
    $lines.Add("title: $($rootNode.Character) $($rootNode.Name) - Uchisen")
    $lines.Add("---")
    $lines.Add("graph LR")
    foreach ($id in $ordered) {
        foreach ($child in $script:nodes[$id].Children) {
            $lines.Add("    $id --> $child")
        }
    }
    $lines.Add("    ")
    for ($i = 0; $i -lt $ordered.Count; $i++) {
        $id = $ordered[$i]
        $node = $script:nodes[$id]
        if ($node.Type -eq 'kanji') {
            $lines.Add("    ${id}[$($node.Character)<br/>$($node.Name)]")
        } elseif ($node.Character) {
            $lines.Add("    ${id}{$($node.Character)<br/>$($node.Name)}")
        } else {
            $lines.Add("    ${id}{$($node.Name)}")
        }
        $lines.Add("    click $id `"$($node.Url)`"")
        if ($i -lt $ordered.Count - 1) {
            $lines.Add("    ")
        }
    }

    $mermaidContent = $lines -join "`n"

    # Output the graph
    if (-not $Path) {
        # No path: write to standard output
        if (-not $isFirst) {
            Write-Output ""
        }
        Write-Output $mermaidContent
    } elseif (Test-Path $resolvedPath -PathType Container) {
        # Directory: create <kanji>-<source>.mermaid file
        $fileName = "$kanjiChar-$Source.mermaid"
        $outputFile = Join-Path $resolvedPath $fileName
        [System.IO.File]::WriteAllText($outputFile, $mermaidContent, [System.Text.UTF8Encoding]::new($false))
        Write-Verbose "Written to $outputFile"
    } elseif ($resolvedPath -like '*.mermaid') {
        # Mermaid file: write/append graph to file
        if ($isFirst) {
            [System.IO.File]::WriteAllText($resolvedPath, $mermaidContent, [System.Text.UTF8Encoding]::new($false))
        } else {
            [System.IO.File]::AppendAllText($resolvedPath, ("`n`n" + $mermaidContent), [System.Text.UTF8Encoding]::new($false))
        }
        Write-Verbose "Written to $resolvedPath"
    } elseif ($resolvedPath -like '*.md') {
        # Markdown file: append with preceding newline and code block
        $mdBlock = "`n" + '```mermaid' + "`n" + $mermaidContent + "`n" + '```' + "`n"
        [System.IO.File]::AppendAllText($resolvedPath, $mdBlock, [System.Text.UTF8Encoding]::new($false))
        Write-Verbose "Appended to $resolvedPath"
    } else {
        throw "Unsupported output path '$resolvedPath'. Must be a directory, .mermaid file, or .md file."
    }

    $isFirst = $false
}

# Write updated primes back to YAML (preserves existing entries, adds new ones)
$yamlLines = [System.Collections.Generic.List[string]]::new()
foreach ($key in $script:primeChars.Keys) {
    $val = $script:primeChars[$key]
    if ($val) {
        $yamlLines.Add("${key}: $val")
    } else {
        $yamlLines.Add("${key}:")
    }
}
$yamlLines.Add('')
[System.IO.File]::WriteAllLines($script:primesFile, $yamlLines, [System.Text.UTF8Encoding]::new($false))
