[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Character,

    [ValidateSet('wanikani', 'uchisen', 'jpdb', 'kanjisense')]
    [string[]]$Source,

    [string]$Path,

    [ValidateSet('Auto', 'Mermaid', 'Markdown')]
    [string]$OutputType = 'Auto',

    [string]$WaniKaniApiToken,

    [switch]$NoCache,

    [switch]$SourceProperties,

    [switch]$NumericPrefix
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

# Web cache directory
$script:webCacheDir = Join-Path $PSScriptRoot '.web-cache'
if (-not $NoCache -and -not (Test-Path $script:webCacheDir)) {
    New-Item -ItemType Directory -Path $script:webCacheDir -Force | Out-Null
}

# Track last request time per host to throttle requests
$script:lastRequestTime = @{}
$script:throttleDelayMs = 1000

function Get-CacheFilePath {
    param([string]$Uri)
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Uri)
    $hash = [System.Security.Cryptography.SHA256]::HashData($bytes)
    $hex = ([System.BitConverter]::ToString($hash) -replace '-', '').ToLowerInvariant()
    return Join-Path $script:webCacheDir $hex
}

function Invoke-WebRequestWithRetry {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [hashtable]$Headers,
        [switch]$ParseJson,
        [int]$MaxRetries = 5,
        [int]$BaseDelayMs = 5000
    )

    # Check cache
    if (-not $NoCache) {
        $cacheFile = Get-CacheFilePath $Uri
        if (Test-Path $cacheFile) {
            Write-Verbose "Cache hit for $Uri"
            $cached = [System.IO.File]::ReadAllText($cacheFile, [System.Text.UTF8Encoding]::new($false))
            if ($ParseJson) {
                return ($cached | ConvertFrom-Json)
            }
            $result = [PSCustomObject]@{ Content = $cached }
            return $result
        }
    }

    # Throttle: ensure minimum delay between requests to the same host
    $host_ = ([Uri]$Uri).Host
    if ($script:lastRequestTime.ContainsKey($host_)) {
        $elapsed = ([DateTime]::UtcNow - $script:lastRequestTime[$host_]).TotalMilliseconds
        if ($elapsed -lt $script:throttleDelayMs) {
            Start-Sleep -Milliseconds ([int]($script:throttleDelayMs - $elapsed))
        }
    }

    for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
        try {
            $script:lastRequestTime[$host_] = [DateTime]::UtcNow
            if ($ParseJson) {
                $response = Invoke-RestMethod -Uri $Uri -Headers $Headers -ProgressAction SilentlyContinue
                if (-not $NoCache) {
                    $response | ConvertTo-Json -Depth 20 | Set-Content -Path $cacheFile -Encoding utf8NoBOM -NoNewline
                }
                return $response
            } else {
                $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -ProgressAction SilentlyContinue
                if (-not $NoCache) {
                    [System.IO.File]::WriteAllText($cacheFile, $response.Content, [System.Text.UTF8Encoding]::new($false))
                }
                return $response
            }
        } catch {
            if ($attempt -eq $MaxRetries) { throw }
            # Only retry on likely transient errors (rate limiting, server errors)
            $statusCode = $_.Exception.Response.StatusCode.value__
            if ($statusCode -and $statusCode -lt 429 -and $statusCode -ne 408) { throw }
            $delay = $BaseDelayMs * [Math]::Pow(2, $attempt - 1)
            Write-Verbose "Request to $Uri failed (attempt $attempt/$MaxRetries): $($_.Exception.Message). Retrying in $($delay / 1000)s..."
            Start-Sleep -Milliseconds $delay
        }
    }
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
    $html = (Invoke-WebRequestWithRetry -Uri $url).Content

    # Extract kanji name(e.g., "知 - Know" or "口 - Mouth, Entrance")
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
    param(
        [string]$KanjiChar,
        [string]$OriginatingRadicalId,
        [System.Collections.Generic.HashSet[string]]$AncestorKanjiIds
    )

    $encodedChar = [Uri]::EscapeDataString($KanjiChar)
    $kanjiUrl = "https://www.wanikani.com/kanji/$encodedChar"

    $apiHeaders = @{
        'Wanikani-Revision' = '20170710'
        'Authorization'     = "Bearer $script:waniKaniToken"
    }

    Write-Verbose "Fetching WaniKani kanji data for $KanjiChar"
    $response = Invoke-WebRequestWithRetry -Uri "https://api.wanikani.com/v2/subjects?types=kanji&slugs=$encodedChar" -Headers $apiHeaders -ParseJson

    if ($response.total_count -eq 0) {
        throw "Kanji '$KanjiChar' not found on WaniKani"
    }

    $kanjiData = $response.data[0].data
    $name = ($kanjiData.meanings | Where-Object { $_.primary }).meaning
    $rootId = Get-NodeId $name
    $componentIds = $kanjiData.component_subject_ids

    # Track ancestor kanji to prevent back-edges
    if (-not $AncestorKanjiIds) {
        $AncestorKanjiIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    }
    $AncestorKanjiIds.Add($rootId) | Out-Null

    $children = [System.Collections.Generic.List[string]]::new()

    if ($componentIds.Count -gt 0) {
        $idsParam = $componentIds -join ','
        Write-Verbose "Fetching WaniKani radical data for IDs: $idsParam"
        $radResponse = Invoke-WebRequestWithRetry -Uri "https://api.wanikani.com/v2/subjects?ids=$idsParam" -Headers $apiHeaders -ParseJson

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
            # or if it references the radical that introduced this kanji (prevent back-edge)
            if ($radId -ceq $rootId) { continue }
            if ($OriginatingRadicalId -and $radId -ceq $OriginatingRadicalId) { continue }

            if (-not $script:nodes.ContainsKey($radId)) {
                # Check if radical's character is also a standalone kanji (recurse)
                $kanjiNodeId = $null
                if ($radChar -and $radId -cne $OriginatingRadicalId) {
                    $radEncodedChar = [Uri]::EscapeDataString($radChar)
                    $kanjiCheckResponse = Invoke-WebRequestWithRetry -Uri "https://api.wanikani.com/v2/subjects?types=kanji&slugs=$radEncodedChar" -Headers $apiHeaders -ParseJson
                    if ($kanjiCheckResponse.total_count -gt 0) {
                        # Check if the kanji would be an ancestor (back-edge); peek at its name
                        $peekName = ($kanjiCheckResponse.data[0].data.meanings | Where-Object { $_.primary }).meaning
                        $peekId = Get-NodeId $peekName
                        if (-not $AncestorKanjiIds.Contains($peekId)) {
                            Write-Verbose "Radical '$radName' ($radChar) is also a kanji, recursing"
                            $kanjiNodeId = Get-WaniKaniDecomposition -KanjiChar $radChar -OriginatingRadicalId $radId -AncestorKanjiIds $AncestorKanjiIds
                            if ($kanjiNodeId -ceq $radId) {
                                # Same name: rename the kanji node to avoid collision with radical
                                $newKanjiId = "${kanjiNodeId}_kanji"
                                $kanjiNode = $script:nodes[$kanjiNodeId]
                                $script:nodes.Remove($kanjiNodeId)
                                $kanjiNode.NodeId = $newKanjiId
                                $script:nodes[$newKanjiId] = $kanjiNode
                                $kanjiNodeId = $newKanjiId
                            }
                        }
                    }
                }

                $radChildren = [System.Collections.Generic.List[string]]::new()
                if ($kanjiNodeId) {
                    $script:dashedEdges["$radId-->$kanjiNodeId"] = $true
                    $radChildren.Add($kanjiNodeId)
                }
                $script:nodes[$radId] = @{
                    NodeId    = $radId
                    Character = if ($radChar) { $radChar } else { '' }
                    Name      = $radName
                    Type      = 'radical'
                    Url       = $radUrl
                    Children  = $radChildren
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
    $html = (Invoke-WebRequestWithRetry -Uri $url).Content

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
    # Strip null bytes (kanjisense bug with supplementary Unicode chars)
    $html = (Invoke-WebRequestWithRetry -Uri $url).Content -replace "`0", ''

    # Extract keyword from the content <h1>(not the site header h1 which contains <a>)
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
}

# Resolve effective output type
$effectiveOutputType = switch ($OutputType) {
    'Mermaid'  { 'Mermaid' }
    'Markdown' { 'Markdown' }
    'Auto' {
        if (-not $Path) { 'Mermaid' }
        elseif (Test-Path $resolvedPath -PathType Container) { 'Mermaid' }
        elseif ($resolvedPath -like '*.md') { 'Markdown' }
        elseif ($resolvedPath -like '*.mermaid') { 'Mermaid' }
        else { 'Mermaid' }
    }
}

# For .mermaid file mode, clear the file first
if ($Path -and $resolvedPath -like '*.mermaid' -and (Test-Path $resolvedPath)) {
    Remove-Item $resolvedPath
}
# For .md file mode, clear the file first
if ($Path -and $resolvedPath -like '*.md' -and (Test-Path $resolvedPath)) {
    Remove-Item $resolvedPath
}

$totalGraphs = $kanjiChars.Count * $Source.Count
$graphIndex = 0
$charIndex = 0
$padWidth = "$($kanjiChars.Count)".Length
if ($padWidth -lt 3) { $padWidth = 3 }
$isFirst = $true
foreach ($kanjiChar in $kanjiChars) {
    $charIndex++
    $filePrefix = if ($NumericPrefix) { "$($charIndex.ToString().PadLeft($padWidth, '0'))-" } else { '' }
    $isFirstSourceForChar = $true
    # For directory + Markdown, collect all sources into one file per character
    $mdCharContent = ''
    foreach ($currentSource in $Source) {
        $graphIndex++
        Write-Progress -Activity "Generating decomposition graphs" -Status "$kanjiChar ($currentSource)" -PercentComplete ([int]($graphIndex / $totalGraphs * 100))

        $sourceDisplayName = $sourceDisplayNames[$currentSource]
        $script:nodes = [System.Collections.Hashtable]::new([System.StringComparer]::Ordinal)
        $script:dashedEdges = [System.Collections.Hashtable]::new([System.StringComparer]::Ordinal)

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
                $edgeKey = "$id-->$child"
                if ($script:dashedEdges.ContainsKey($edgeKey)) {
                    $lines.Add("    $id -.-> $child")
                } else {
                    $lines.Add("    $id --> $child")
                }
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
        $fence = '```'

        # Output the graph
        $isDir = $Path -and (Test-Path $resolvedPath -PathType Container)
        if ($isDir -and $effectiveOutputType -eq 'Markdown') {
            # Directory + Markdown: collect into per-character file (written after inner loop)
            $mdCharContent += "`n${fence}mermaid`n" + $mermaidContent + "`n$fence`n"
        } elseif ($isDir) {
            # Directory + Mermaid: one file per character/source
            $fileName = "${filePrefix}$kanjiChar-$currentSource.mermaid"
            $outputFile = Join-Path $resolvedPath $fileName
            [System.IO.File]::WriteAllText($outputFile, $mermaidContent, [System.Text.UTF8Encoding]::new($false))
            Write-Verbose "Written to $outputFile"
        } elseif (-not $Path) {
            # No path: write to standard output
            if ($effectiveOutputType -eq 'Markdown') {
                $content = "${fence}mermaid`n" + $mermaidContent + "`n$fence"
                if (-not $isFirst) { Write-Output "" }
                Write-Output $content
            } else {
                if (-not $isFirst) { Write-Output "" }
                Write-Output $mermaidContent
            }
        } elseif ($resolvedPath -like '*.mermaid') {
            # Mermaid file: write/append graph
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
            $mdBlock += "`n${fence}mermaid`n" + $mermaidContent + "`n$fence`n"
            [System.IO.File]::AppendAllText($resolvedPath, $mdBlock, [System.Text.UTF8Encoding]::new($false))
            Write-Verbose "Appended to $resolvedPath"
        } else {
            throw "Unsupported output path '$resolvedPath'. Must be a directory, .mermaid file, or .md file."
        }

        $isFirst = $false
        $isFirstSourceForChar = $false
    }

    # Write per-character markdown file for directory + Markdown mode
    if ($Path -and (Test-Path $resolvedPath -PathType Container) -and $effectiveOutputType -eq 'Markdown') {
        $fileName = "${filePrefix}$kanjiChar.md"
        $outputFile = Join-Path $resolvedPath $fileName
        # Build frontmatter if -SourceProperties is specified
        $fileContent = ''
        if ($SourceProperties) {
            $fmLines = [System.Collections.Generic.List[string]]::new()
            $fmLines.Add('---')
            foreach ($s in $Source) {
                $fmLines.Add("${s}:")
            }
            $fmLines.Add('---')
            $fileContent = ($fmLines -join "`n") + "`n"
        }
        $fileContent += $mdCharContent
        [System.IO.File]::WriteAllText($outputFile, $fileContent, [System.Text.UTF8Encoding]::new($false))
        Write-Verbose "Written to $outputFile"
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
