[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath,

    [Parameter(Mandatory)]
    [string]$OutputPath,

    [Parameter()]
    [string]$ImdbDataPath = (Join-Path ([IO.Path]::GetTempPath()) 'tandoku-imdb'),

    [switch]$UpdateImdbData
)

Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"

$sparqlHeaders = @{ "User-Agent" = "tandoku-discover/1.0 (https://github.com/tandoku)" }

# IMDb title types that represent watchable films / series (excludes tvEpisode, videoGame, etc.)
$allowedTitleTypes = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@('movie', 'tvMovie', 'tvSeries', 'tvMiniSeries', 'short', 'tvShort', 'tvSpecial', 'video'))

# Rank of title type when choosing between multiple candidates (lower = preferred)
$titleTypeRank = @{
    movie        = 0
    tvSeries     = 0
    tvMiniSeries = 0
    tvMovie      = 1
    tvSpecial    = 2
    video        = 3
    short        = 4
    tvShort      = 4
}

function Invoke-WikidataSparql($query) {
    $url = "https://query.wikidata.org/sparql?query=$([uri]::EscapeDataString($query))&format=json"
    $maxRetries = 3
    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
        try {
            return (Invoke-RestMethod -Uri $url -Headers $sparqlHeaders).results.bindings
        }
        catch {
            $retrySeconds = 0
            if ($_.Exception.Response.StatusCode -eq 429 -or $_ -match '429') {
                if ($_ -match 'retry in (\d+) seconds') {
                    $retrySeconds = [int]$Matches[1]
                } else {
                    $retrySeconds = 120
                }
            }

            if ($retrySeconds -gt 0 -and $attempt -lt $maxRetries) {
                Write-Warning "Rate limited (429) - waiting $retrySeconds seconds before retry $attempt/$maxRetries..."
                Start-Sleep -Seconds $retrySeconds
            } else {
                throw
            }
        }
    }
}

function Get-NormalizedTitle([string]$title) {
    if ([string]::IsNullOrEmpty($title) -or $title -eq '\N') {
        return $null
    }
    $n = $title.ToLowerInvariant()
    $n = [regex]::Replace($n, '[^\p{L}\p{Nd}]+', ' ')
    return $n.Trim()
}

# Japanese-language signal: title contains Hiragana, Katakana, or CJK ideographs
$cjkRegex = [regex]::new('[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]')
function Test-ContainsJapanese([string]$title) {
    if ([string]::IsNullOrEmpty($title) -or $title -eq '\N') {
        return $false
    }
    return $cjkRegex.IsMatch($title)
}

# --- Read films database, collect entries that need a Wikidata identifier ---

$films = [System.Collections.Generic.List[object]]::new()
foreach ($doc in @(Import-Yaml -LiteralPath $DatabasePath)) {
    $films.Add($doc)
}

Write-Host "Read $($films.Count) entries from films database"

# Map normalized Netflix title -> list of films missing wikidata (a title may map to several films)
$filmsByTitle = @{}
$needsWikidata = [System.Collections.Generic.List[object]]::new()
foreach ($film in $films) {
    if ($film.providers -and $film.providers.netflix -and $null -ne $film.providers.netflix.id -and -not $film.wikidata) {
        $needsWikidata.Add($film)
        $norm = Get-NormalizedTitle ([string]$film.providers.netflix.title)
        if ($norm) {
            if (-not $filmsByTitle.ContainsKey($norm)) {
                $filmsByTitle[$norm] = [System.Collections.Generic.List[object]]::new()
            }
            $filmsByTitle[$norm].Add($film)
        }
    }
}

Write-Host "$($needsWikidata.Count) entries missing Wikidata identifier (with Netflix info)"

foreach ($norm in $filmsByTitle.Keys) {
    if ($filmsByTitle[$norm].Count -gt 1) {
        $ids = ($filmsByTitle[$norm] | ForEach-Object { $_.providers.netflix.id }) -join ', '
        Write-Warning "Multiple films share normalized title '$norm' (Netflix IDs: $ids); IMDb matches will apply to all of them"
    }
}

if ($needsWikidata.Count -eq 0) {
    Write-Host "Nothing to do"
    @() | Export-Yaml -Path $OutputPath
    return
}

# --- Download IMDb datasets ---

$imdbData = & "$PSScriptRoot/UpdateIMDbData.ps1" `
    -ImdbDataPath $ImdbDataPath `
    -Datasets 'title.akas', 'title.basics', 'title.ratings' `
    -UpdateImdbData:$UpdateImdbData

# Candidate IMDb titles keyed by tconst.
#   films          = set of Netflix films this tconst was title-matched to
#   titleType      = IMDb title type (from basics)
#   japaneseAka    = has an alternate title in Japanese (language 'ja' or CJK text)
#   japaneseOrig   = basics originalTitle contains Japanese text
#   votes          = IMDb vote count (tie-breaker)
$candidates = @{}

function Add-Candidate([string]$tconst, $matchedFilms) {
    if (-not $candidates.ContainsKey($tconst)) {
        $candidates[$tconst] = [ordered]@{
            films        = [System.Collections.Generic.Dictionary[object, object]]::new()
            titleType    = $null
            japaneseAka  = $false
            japaneseOrig = $false
            votes        = 0
        }
    }
    foreach ($film in $matchedFilms) {
        $candidates[$tconst].films[$film] = $true
    }
}

# --- Pass 1: title.akas - match alternate titles to needed Netflix titles ---
#
# Rows for a given titleId are contiguous in the file, so we accumulate each group and, on
# encountering a new titleId, decide whether the group title-matched any needed film and whether
# any of its alternate titles is Japanese (a far stronger origin signal than the matched English
# title alone, e.g. the Japanese-language aka can appear in a different row than the match).

Write-Host "Scanning IMDb akas for title matches..."
$akasPath = $imdbData['title.akas']
$lineCount = 0
$groupTconst = $null
$groupFilms = $null
$groupTitles = [System.Collections.Generic.List[string]]::new()
$groupHasJaLang = $false
foreach ($line in [System.IO.File]::ReadLines($akasPath)) {
    $lineCount++
    if ($lineCount -eq 1) { continue } # header
    # titleId, ordering, title, region, language, ...
    $fields = $line.Split("`t", 5)
    if ($fields.Count -lt 3) { continue }
    $tconst = $fields[0]

    if ($tconst -ne $groupTconst) {
        # Flush previous group; evaluate the (relatively expensive) Japanese-script test only for
        # groups that actually matched a needed film, keeping the per-row work cheap.
        if ($groupFilms -and $groupFilms.Count -gt 0) {
            $hasJa = $groupHasJaLang
            if (-not $hasJa) {
                foreach ($t in $groupTitles) {
                    if (Test-ContainsJapanese $t) { $hasJa = $true; break }
                }
            }
            Add-Candidate $groupTconst @($groupFilms.Keys)
            $candidates[$groupTconst].japaneseAka = $hasJa
        }
        $groupTconst = $tconst
        $groupFilms = $null
        $groupTitles.Clear()
        $groupHasJaLang = $false
    }

    $title = $fields[2]
    $groupTitles.Add($title)
    if ($fields.Count -ge 5 -and $fields[4] -eq 'ja') { $groupHasJaLang = $true }
    $norm = Get-NormalizedTitle $title
    if ($norm -and $filmsByTitle.ContainsKey($norm)) {
        if (-not $groupFilms) { $groupFilms = [System.Collections.Generic.Dictionary[object, object]]::new() }
        foreach ($f in $filmsByTitle[$norm]) { $groupFilms[$f] = $true }
    }
}
if ($groupFilms -and $groupFilms.Count -gt 0) {
    $hasJa = $groupHasJaLang
    if (-not $hasJa) {
        foreach ($t in $groupTitles) {
            if (Test-ContainsJapanese $t) { $hasJa = $true; break }
        }
    }
    Add-Candidate $groupTconst @($groupFilms.Keys)
    $candidates[$groupTconst].japaneseAka = $hasJa
}
Write-Host "akas matches: $($candidates.Count) candidate IMDb titles"

# --- Pass 2: title.basics - match primary/original titles, record type + japanese signal ---

Write-Host "Scanning IMDb basics for title matches and metadata..."
$basicsPath = $imdbData['title.basics']
$lineCount = 0
foreach ($line in [System.IO.File]::ReadLines($basicsPath)) {
    $lineCount++
    if ($lineCount -eq 1) { continue } # header
    # tconst, titleType, primaryTitle, originalTitle, ...
    $fields = $line.Split("`t", 5)
    if ($fields.Count -lt 4) { continue }
    $tconst = $fields[0]
    $primaryTitle = $fields[2]
    $originalTitle = $fields[3]

    foreach ($t in @($primaryTitle, $originalTitle)) {
        $norm = Get-NormalizedTitle $t
        if ($norm -and $filmsByTitle.ContainsKey($norm)) {
            Add-Candidate $tconst $filmsByTitle[$norm]
        }
    }

    if ($candidates.ContainsKey($tconst)) {
        $candidates[$tconst].titleType = $fields[1]
        $candidates[$tconst].japaneseOrig = Test-ContainsJapanese $originalTitle
    }
}
Write-Host "After basics: $($candidates.Count) candidate IMDb titles"

# --- Pass 3: title.ratings - votes for tie-breaking ---

Write-Host "Scanning IMDb ratings for vote counts..."
$ratingsPath = $imdbData['title.ratings']
$lineCount = 0
foreach ($line in [System.IO.File]::ReadLines($ratingsPath)) {
    $lineCount++
    if ($lineCount -eq 1) { continue } # header
    # tconst, averageRating, numVotes
    $fields = $line.Split("`t", 4)
    if ($fields.Count -lt 3) { continue }
    if ($candidates.ContainsKey($fields[0])) {
        $candidates[$fields[0]].votes = [int]$fields[2]
    }
}

# --- Filter candidates and assign best IMDb match per film ---

# Invert: Netflix film -> list of surviving candidate tconsts
$filmCandidates = [System.Collections.Generic.Dictionary[object, object]]::new()
foreach ($tconst in $candidates.Keys) {
    $c = $candidates[$tconst]
    if (-not $allowedTitleTypes.Contains([string]$c.titleType)) { continue }
    if (-not ($c.japaneseAka -or $c.japaneseOrig)) { continue }
    foreach ($film in $c.films.Keys) {
        if (-not $filmCandidates.ContainsKey($film)) {
            $filmCandidates[$film] = [System.Collections.Generic.List[object]]::new()
        }
        $filmCandidates[$film].Add(@{ tconst = $tconst; data = $c })
    }
}

# Choose the best candidate per film: preferred title type, then most votes, then lowest tconst
$bestImdbId = [System.Collections.Generic.Dictionary[object, string]]::new()
foreach ($film in $filmCandidates.Keys) {
    # Wrap in @() so a single candidate isn't unwrapped (Sort-Object would otherwise return the
    # lone hashtable, whose .Count is its key count and whose [0] indexer is meaningless)
    $ranked = @($filmCandidates[$film] | Sort-Object `
        @{ Expression = { if ($titleTypeRank.ContainsKey([string]$_.data.titleType)) { $titleTypeRank[[string]$_.data.titleType] } else { 99 } } }, `
        @{ Expression = { $_.data.votes }; Descending = $true }, `
        @{ Expression = { $_.tconst } })
    $best = $ranked[0]
    $bestImdbId[$film] = $best.tconst

    if ($ranked.Count -gt 1) {
        $alts = ($ranked | Select-Object -Skip 1 | ForEach-Object { "$($_.tconst) ($($_.data.titleType), $($_.data.votes) votes)" }) -join ', '
        Write-Warning "Netflix '$($film.providers.netflix.title)' ($($film.providers.netflix.id)) -> $($best.tconst); other candidates: $alts"
    }
}

Write-Host "Found IMDb candidates for $($bestImdbId.Count)/$($needsWikidata.Count) films"

# --- Look up existing Wikidata entities for the matched IMDb IDs (P345) ---

$imdbIds = @($bestImdbId.Values | Sort-Object -Unique)
$qidByImdbId = @{}
if ($imdbIds.Count -gt 0) {
    Write-Host "Querying Wikidata for $($imdbIds.Count) IMDb IDs..."
    $batchSize = 50
    for ($i = 0; $i -lt $imdbIds.Count; $i += $batchSize) {
        $end = [Math]::Min($i + $batchSize - 1, $imdbIds.Count - 1)
        $batch = $imdbIds[$i..$end]
        $values = ($batch | ForEach-Object { "`"$_`"" }) -join " "
        $query = "SELECT ?item ?imdbId WHERE { VALUES ?imdbId { $values } ?item wdt:P345 ?imdbId . }"

        try {
            foreach ($binding in (Invoke-WikidataSparql $query)) {
                $qid = $binding.item.value -replace '.*/entity/', ''
                $imdbId = $binding.imdbId.value
                if (-not $qidByImdbId.ContainsKey($imdbId)) {
                    $qidByImdbId[$imdbId] = $qid
                } elseif ($qidByImdbId[$imdbId] -ne $qid) {
                    Write-Warning "IMDb ID $imdbId matches multiple Wikidata entities: $($qidByImdbId[$imdbId]), $qid (keeping first)"
                }
            }
        }
        catch {
            Write-Warning "Failed to query Wikidata for batch at index ${i}: $_"
        }

        Write-Host "Wikidata lookup: $([Math]::Min($i + $batchSize, $imdbIds.Count))/$($imdbIds.Count)"

        if ($i + $batchSize -lt $imdbIds.Count) {
            Start-Sleep -Milliseconds 1000
        }
    }
}

# --- Write candidates to output ---

$results = [System.Collections.Generic.List[object]]::new()
$withImdb = 0
$withWikidata = 0
foreach ($film in $needsWikidata) {
    $netflixId = $film.providers.netflix.id
    $entry = [ordered]@{
        netflix = [ordered]@{
            id  = $netflixId
            url = "https://www.netflix.com/title/$netflixId"
        }
    }

    if ($bestImdbId.ContainsKey($film)) {
        $imdbId = $bestImdbId[$film]
        $entry['imdb'] = [ordered]@{
            id  = $imdbId
            url = "https://www.imdb.com/title/$imdbId/"
        }
        $withImdb++

        if ($qidByImdbId.ContainsKey($imdbId)) {
            $qid = $qidByImdbId[$imdbId]
            $entry['wikidata'] = [ordered]@{
                id  = $qid
                url = "https://www.wikidata.org/wiki/$qid"
            }
            $withWikidata++
        }
    }

    $results.Add($entry)
}

$results | Export-Yaml -Path $OutputPath

Write-Host "Done - wrote $($results.Count) candidates to $OutputPath"
Write-Host "  $withImdb with IMDb match, $withWikidata with existing Wikidata entity"
