[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Character,

    [ValidateSet('wanikani', 'uchisen', 'jpdb', 'kanjisense')]
    [string[]]$Source,

    [string]$Path,

    [string]$WaniKaniApiToken
)

$ErrorActionPreference = 'Stop'

# Source display names
$sourceDisplayNames = @{
    'uchisen'    = 'uchisen'
    'wanikani'   = 'WaniKani'
    'jpdb'       = 'jpdb.io'
    'kanjisense' = 'kanjisense'
}

# Default to all sources if not specified
$allSources = @('wanikani', 'uchisen', 'jpdb', 'kanjisense')
if (-not $Source) { $Source = $allSources }

# Load prime Unicode characters from uchisen-primes.yaml (if uchisen is a selected source)
$script:primesFile = Join-Path $PSScriptRoot 'uchisen-primes.yaml'
$script:primeChars = [ordered]@{}
if ($Source -contains 'uchisen' -and (Test-Path $script:primesFile)) {
    foreach ($line in [System.IO.File]::ReadAllLines($script:primesFile, [System.Text.UTF8Encoding]::new($false))) {
        if ($line -match '^([^#:]+):\s*(.*)$') {
            $script:primeChars[$Matches[1].Trim()] = $Matches[2].Trim()
        }
    }
}

# Resolve WaniKani API token
if ($Source -contains 'wanikani') {
    $script:waniKaniToken = if ($WaniKaniApiToken) { $WaniKaniApiToken } else { $env:WANIKANI_API_TOKEN }
    if (-not $script:waniKaniToken) {
        throw "WaniKani API token required. Pass -WaniKaniApiToken or set WANIKANI_API_TOKEN environment variable."
    }
}

function Get-NodeId {
    param([string]$Name)
    $first = ($Name -split '[,;]')[0].Trim()
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

function Get-WaniKaniDecomposition {
    param([string]$KanjiChar)

    $encodedChar = [Uri]::EscapeDataString($KanjiChar)
    $kanjiUrl = "https://www.wanikani.com/kanji/$encodedChar"

    $apiHeaders = @{
        'Wanikani-Revision' = '20170710'
        'Authorization'     = "Bearer $script:waniKaniToken"
    }

    Write-Verbose "Fetching WaniKani kanji data for $KanjiChar"
    $response = Invoke-RestMethod -Uri "https://api.wanikani.com/v2/subjects?types=kanji&slugs=$encodedChar" -Headers $apiHeaders

    if ($response.total_count -eq 0) {
        throw "Kanji '$KanjiChar' not found on WaniKani"
    }

    $kanjiData = $response.data[0].data
    $name = ($kanjiData.meanings | Where-Object { $_.primary }).meaning
    $rootId = Get-NodeId $name
    $componentIds = $kanjiData.component_subject_ids

    $children = [System.Collections.Generic.List[string]]::new()

    if ($componentIds.Count -gt 0) {
        $idsParam = $componentIds -join ','
        Write-Verbose "Fetching WaniKani radical data for IDs: $idsParam"
        $radResponse = Invoke-RestMethod -Uri "https://api.wanikani.com/v2/subjects?ids=$idsParam" -Headers $apiHeaders

        # Build lookup by subject ID, preserving order from component_subject_ids
        $radLookup = @{}
        foreach ($rad in $radResponse.data) {
            $radLookup[$rad.id] = $rad
        }

        foreach ($compId in $componentIds) {
            $rad = $radLookup[$compId]
            $radData = $rad.data
            $radName = ($radData.meanings | Where-Object { $_.primary }).meaning
            $radChar = $radData.characters
            $radSlug = $radData.slug
            $radId = Get-NodeId $radName
            $radUrl = "https://www.wanikani.com/radicals/$radSlug"

            # Skip radical if it has same node ID as root kanji (self-decomposition)
            if ($radId -ceq $rootId) { continue }

            if (-not $script:nodes.ContainsKey($radId)) {
                $script:nodes[$radId] = @{
                    NodeId    = $radId
                    Character = if ($radChar) { $radChar } else { '' }
                    Name      = $radName
                    Type      = 'radical'
                    Url       = $radUrl
                    Children  = [System.Collections.Generic.List[string]]::new()
                }
            }
            $children.Add($radId)
        }
    }

    $script:nodes[$rootId] = @{
        NodeId    = $rootId
        Character = $KanjiChar
        Name      = $name
        Type      = 'kanji'
        Url       = $kanjiUrl
        Children  = $children
    }

    return $rootId
}

function Get-JpdbDecomposition {
    param([string]$KanjiChar)

    $encodedChar = [Uri]::EscapeDataString($KanjiChar)
    $url = "https://jpdb.io/kanji/$encodedChar"

    # Return existing node ID if already processed
    foreach ($n in $script:nodes.Values) {
        if ($n.Url -eq $url) { return $n.NodeId }
    }

    Write-Verbose "Fetching decomposition for $KanjiChar from $url"
    $response = Invoke-WebRequest -Uri $url -UseBasicParsing
    $html = $response.Content

    # Extract keyword
    if ($html -notmatch '<h6 class="subsection-label">Keyword</h6>\s*<div class="subsection">([^<]+)</div>') {
        throw "Could not extract keyword for character '$KanjiChar' from $url"
    }
    $name = $Matches[1].Trim()
    $nodeId = Get-NodeId $name

    $children = [System.Collections.Generic.List[string]]::new()

    # Extract the first "Composed of" section
    $compIdx = $html.IndexOf('>Composed of</h6>')
    if ($compIdx -ge 0) {
        $sectionStart = $html.IndexOf('<div class="subsection">', $compIdx)
        if ($sectionStart -ge 0) {
            # Find end by looking for the next section heading
            $sectionEnd = $html.IndexOf('class="subsection-label"', $sectionStart)
            if ($sectionEnd -lt 0) { $sectionEnd = [Math]::Min($sectionStart + 2000, $html.Length) }
            $compHtml = $html.Substring($sectionStart, $sectionEnd - $sectionStart)

            # Extract components: <a class="plain" href="/kanji/X#a">CHAR</a> followed by <div class="description">NAME</div>
            foreach ($m in [regex]::Matches($compHtml, 'href="/kanji/([^"#]+)#a">([^<]+)</a></div>\s*<div class="description">([^<]+)</div>')) {
                $childChar = $m.Groups[2].Value.Trim()
                $childName = $m.Groups[3].Value.Trim()

                # Determine type: standard CJK = kanji (recurse), otherwise = prime (leaf)
                $code = [int][char]$childChar[0]
                if ($childChar.Length -eq 1 -and (($code -ge 0x4E00 -and $code -le 0x9FFF) -or ($code -ge 0x3400 -and $code -le 0x4DBF))) {
                    $childId = Get-JpdbDecomposition -KanjiChar $childChar
                } else {
                    $childId = Get-NodeId $childName
                    if (-not $script:nodes.ContainsKey($childId)) {
                        $childEncoded = [Uri]::EscapeDataString($childChar)
                        $script:nodes[$childId] = @{
                            NodeId    = $childId
                            Character = $childChar
                            Name      = $childName
                            Type      = 'prime'
                            Url       = "https://jpdb.io/kanji/$childEncoded"
                            Children  = [System.Collections.Generic.List[string]]::new()
                        }
                    }
                }
                $children.Add($childId)
            }
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

function Get-KanjisenseDecomposition {
    param([string]$DictPath)

    $encodedPath = [Uri]::EscapeDataString($DictPath)
    $url = "https://kanjisense.com/dict/$encodedPath"

    # Return existing node ID if already processed
    foreach ($n in $script:nodes.Values) {
        if ($n.Url -eq $url) { return $n.NodeId }
    }

    # Determine if this is a displayable character or a code identifier (CDP-*, GWS-*)
    $isCodeId = $DictPath -match '^[A-Za-z]'
    $displayChar = if ($isCodeId) { '' } else { $DictPath }

    Write-Verbose "Fetching decomposition for $DictPath from $url"
    $response = Invoke-WebRequest -Uri $url -UseBasicParsing
    $html = $response.Content

    # Extract keyword from the content <h1> (not the site header h1 which contains <a>)
    if ($html -match 'text-xl\s*"><h1>(.*?)</h1>') {
        $rawName = $Matches[1]
        # Strip HTML tags and comments, then clean up component mnemonic prefix and cf. suffix
        $name = ($rawName -replace '<!--.*?-->', '' -replace '<[^>]+>', '').Trim()
        $name = ($name -replace '^component mnemonic:\s*', '').Trim()
        $name = ($name -replace '\s*\(cf\..*?\)', '').Trim()
        $name = ($name -replace '\s*\(via\s.*?\)', '').Trim()
    } else {
        throw "Could not extract keyword for '$DictPath' from $url"
    }
    $nodeId = Get-NodeId $name

    # Detect type: "component only" entries use diamond shape
    $type = if ($html -match '>○ component only<') { 'component' } else { 'kanji' }

    $children = [System.Collections.Generic.List[string]]::new()

    # Extract the "components" section
    $compIdx = $html.IndexOf('>components</h2>')
    if ($compIdx -ge 0) {
        # Find the section element containing components
        $sectionStart = $html.IndexOf('<section', $compIdx)
        if ($sectionStart -ge 0) {
            $sectionEnd = $html.IndexOf('</section>', $sectionStart)
            if ($sectionEnd -lt 0) { $sectionEnd = [Math]::Min($sectionStart + 5000, $html.Length) }
            $compHtml = $html.Substring($sectionStart, $sectionEnd - $sectionStart)

            # Extract component paths from href="/dict/PATH"
            foreach ($m in [regex]::Matches($compHtml, 'href="/dict/([^"]+)"')) {
                $childPath = [Uri]::UnescapeDataString($m.Groups[1].Value)
                $childId = Get-KanjisenseDecomposition -DictPath $childPath
                $children.Add($childId)
            }
        }
    }

    $script:nodes[$nodeId] = @{
        NodeId    = $nodeId
        Character = $displayChar
        Name      = $name
        Type      = $type
        Url       = $url
        Children  = $children
    }

    return $nodeId
}

# Filter to kanji characters only(CJK Unified Ideographs + Extension A)
$kanjiChars = $Character.ToCharArray() | Where-Object {
    $code = [int]$_
    ($code -ge 0x4E00 -and $code -le 0x9FFF) -or ($code -ge 0x3400 -and $code -le 0x4DBF)
}

if (-not $kanjiChars) {
    throw "No kanji characters found in input '$Character'"
}

if ($Path) {
    $resolvedPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    # For .mermaid file mode, clear the file first
    if ($resolvedPath -like '*.mermaid' -and (Test-Path $resolvedPath)) {
        Remove-Item $resolvedPath
    }
}

$totalGraphs = $kanjiChars.Count * $Source.Count
$graphIndex = 0
$isFirst = $true
foreach ($kanjiChar in $kanjiChars) {
    $isFirstSourceForChar = $true
    foreach ($currentSource in $Source) {
        $graphIndex++
        Write-Progress -Activity "Generating decomposition graphs" -Status "$kanjiChar ($currentSource)" -PercentComplete ([int]($graphIndex / $totalGraphs * 100))

        $sourceDisplayName = $sourceDisplayNames[$currentSource]
        $script:nodes = [System.Collections.Hashtable]::new([System.StringComparer]::Ordinal)

        # Perform decomposition using the selected source
        $rootId = switch ($currentSource) {
            'uchisen'    { Get-UchisenDecomposition -KanjiChar $kanjiChar }
            'wanikani'   { Get-WaniKaniDecomposition -KanjiChar $kanjiChar }
            'jpdb'       { Get-JpdbDecomposition -KanjiChar $kanjiChar }
            'kanjisense' { Get-KanjisenseDecomposition -DictPath $kanjiChar }
        }

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
        $lines.Add("title: $($rootNode.Character) $($rootNode.Name) - $sourceDisplayName")
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
            } elseif ($node.Type -in 'prime', 'radical', 'component') {
                if ($node.Character) {
                    $lines.Add("    ${id}{$($node.Character)<br/>$($node.Name)}")
                } else {
                    $lines.Add("    ${id}{$($node.Name)}")
                }
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
            $fileName = "$kanjiChar-$currentSource.mermaid"
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
            # Markdown file: add character heading when using multiple sources, then code block
            $mdBlock = ''
            if ($Source.Count -gt 1 -and $isFirstSourceForChar) {
                $mdBlock += "`n# $kanjiChar`n"
            }
            $fence = '```'
            $mdBlock += "`n${fence}mermaid`n" + $mermaidContent + "`n$fence`n"
            [System.IO.File]::AppendAllText($resolvedPath, $mdBlock, [System.Text.UTF8Encoding]::new($false))
            Write-Verbose "Appended to $resolvedPath"
        } else {
            throw "Unsupported output path '$resolvedPath'. Must be a directory, .mermaid file, or .md file."
        }

        $isFirst = $false
        $isFirstSourceForChar = $false
    }
}
Write-Progress -Activity "Generating decomposition graphs" -Completed

# Write updated primes back to YAML (if uchisen was a selected source)
if ($Source -contains 'uchisen') {
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
}
