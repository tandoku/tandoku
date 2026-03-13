[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Character,

    [Parameter(Mandatory)]
    [ValidateSet('uchisen')]
    [string]$Source,

    [Parameter(Mandatory)]
    [string]$Path
)

$ErrorActionPreference = 'Stop'

$script:nodes = @{}

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
        if ($endIdx -lt 0) { $endIdx = [Math]::Min($compIdx + 5000, $html.Length) }
        $compHtml = $html.Substring($compIdx, $endIdx - $compIdx)

        # Extract primes (listed before compound kanji in the HTML)
        foreach ($m in [regex]::Matches($compHtml, 'href="/primes/([^"]+)"[^>]*>([^:]+):')) {
            $primePath = $m.Groups[1].Value
            $primeName = $m.Groups[2].Value.Trim()
            $primeId = Get-NodeId $primeName

            if (-not $script:nodes.ContainsKey($primeId)) {
                $script:nodes[$primeId] = @{
                    NodeId    = $primeId
                    Character = ''
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

# Perform recursive decomposition
$rootId = Get-UchisenDecomposition -KanjiChar $Character

# BFS traversal for output ordering
$ordered = [System.Collections.Generic.List[string]]::new()
$queue = [System.Collections.Queue]::new()
$visited = @{}
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
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("graph LR")
$lines.Add("    Uchisen(Uchisen) --> $rootId")
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

# Determine output based on path type
$resolvedPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)

if (Test-Path $resolvedPath -PathType Container) {
    # Directory: create <kanji>-<source>.mermaid file
    $fileName = "$Character-$Source.mermaid"
    $outputFile = Join-Path $resolvedPath $fileName
    [System.IO.File]::WriteAllText($outputFile, $mermaidContent, [System.Text.UTF8Encoding]::new($false))
    Write-Verbose "Written to $outputFile"
} elseif ($resolvedPath -like '*.mermaid') {
    # Mermaid file: write graph to file
    [System.IO.File]::WriteAllText($resolvedPath, $mermaidContent, [System.Text.UTF8Encoding]::new($false))
    Write-Verbose "Written to $resolvedPath"
} elseif ($resolvedPath -like '*.md') {
    # Markdown file: append with preceding newline and code block
    $mdBlock = "`n" + '```mermaid' + "`n" + $mermaidContent + "`n" + '```' + "`n"
    [System.IO.File]::AppendAllText($resolvedPath, $mdBlock, [System.Text.UTF8Encoding]::new($false))
    Write-Verbose "Appended to $resolvedPath"
} else {
    throw "Unsupported output path '$resolvedPath'. Must be a directory, .mermaid file, or .md file."
}
