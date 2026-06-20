[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath,

    [Parameter(Mandatory)]
    [string]$IMDbListUrl
)

Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"
Import-Module "$PSScriptRoot/tandoku-discover-films.psm1"

# Browser-like User-Agent; IMDb serves an AWS WAF JavaScript challenge to
# requests it considers bot-like (e.g. plain HTTP clients from datacenter IPs).
$script:UserAgent = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'

# Extracts the list id (lsNNNNNNNNN) from an IMDb list URL.
function Get-ListId([string]$url) {
    if ($url -match '(ls\d+)') {
        return $Matches[1]
    }
    throw "Could not find an IMDb list id (lsNNNN...) in URL: $url"
}

# Fetches a single IMDb list page and returns its HTML. Detects the AWS WAF
# challenge interstitial (which contains no list data) and fails with guidance.
function Get-ListPageHtml([string]$url) {
    $response = Invoke-WebRequest -Uri $url -UserAgent $script:UserAgent -Headers @{
        'Accept'          = 'text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8'
        'Accept-Language' = 'en-US,en;q=0.9'
    }
    $html = $response.Content
    if ($html -match 'awswaf' -or $html -match 'challenge-container') {
        throw "IMDb returned an anti-bot challenge page instead of the list (URL: $url). Try again from a network/IP that IMDb trusts, or open the list in a browser first."
    }
    return $html
}

# Parses the application/ld+json ItemList block out of an IMDb list page,
# returning a hashtable with the list Name and an array of Items (each with
# Id, Title and Rank).
function Get-ListData([string]$html) {
    $pattern = '<script[^>]*type="application/ld\+json"[^>]*>(.*?)</script>'
    $matches = [regex]::Matches($html, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)

    $itemList = $null
    foreach ($m in $matches) {
        $json = $m.Groups[1].Value
        try {
            $obj = $json | ConvertFrom-Json -AsHashtable
        } catch {
            continue
        }
        $type = $obj['@type']
        if (($type -is [array] -and ($type -contains 'ItemList')) -or ($type -eq 'ItemList')) {
            $itemList = $obj
            break
        }
    }

    if (-not $itemList) {
        throw 'Could not find an ItemList JSON-LD block in the IMDb list page.'
    }

    $name = $itemList['name']
    if (-not $name) {
        throw 'IMDb list JSON-LD did not include a list name.'
    }

    $items = [System.Collections.Generic.List[object]]::new()
    foreach ($element in @($itemList['itemListElement'])) {
        # Newer IMDb markup nests the title under `item`; older markup puts the
        # url/name directly on the ListItem.
        $node = if ($element.Contains('item')) { $element['item'] } else { $element }

        $url = $node['url']
        if (-not $url -or $url -notmatch '(tt\d+)') { continue }
        $id = $Matches[1]

        $title = $node['name']
        if (-not $title) { $title = $node['title'] }

        $items.Add([pscustomobject]@{
            Id    = $id
            Title = $title
            Rank  = [int]$element['position']
        })
    }

    return @{
        Name           = [string]$name
        NumberOfItems  = $itemList['numberOfItems']
        Items          = $items
    }
}

# --- Fetch the list (paging until no new titles are seen) ---

$listId = Get-ListId $IMDbListUrl
$baseUrl = "https://www.imdb.com/list/$listId/"

$listName = $null
$declaredCount = $null
$seenIds = [System.Collections.Generic.HashSet[string]]::new()
$listItems = [System.Collections.Generic.List[object]]::new()

$page = 1
while ($true) {
    $pageUrl = "${baseUrl}?page=$page"
    Write-Host "Fetching $pageUrl"
    $html = Get-ListPageHtml $pageUrl
    $data = Get-ListData $html

    if (-not $listName) { $listName = $data.Name }
    if (-not $declaredCount -and $data.NumberOfItems) { $declaredCount = [int]$data.NumberOfItems }

    $newOnPage = 0
    foreach ($item in $data.Items) {
        if ($seenIds.Add($item.Id)) {
            $listItems.Add($item)
            $newOnPage++
        }
    }

    Write-Host "  Page $page contributed $newOnPage new title(s) (total $($listItems.Count))"

    # Stop when a page adds nothing new: either the list ended, or IMDb ignored
    # the page parameter and re-served the first page.
    if ($newOnPage -eq 0) { break }
    $page++
}

Write-Host "List '$listName' ($listId): $($listItems.Count) title(s)"
if ($declaredCount -and $listItems.Count -lt $declaredCount) {
    Write-Warning "IMDb reports $declaredCount items in this list but only $($listItems.Count) were extracted; some titles may not have been imported."
}

# --- Read existing films database ---

$films = Read-FilmsDatabase -LiteralPath $DatabasePath -AllowMissing

Write-Host "Read $($films.Count) existing entries from films database"

# Build lookup table for existing films by IMDb ID
$filmsByImdbId = @{}
for ($i = 0; $i -lt $films.Count; $i++) {
    $film = $films[$i]
    if ($film.imdb -and $film.imdb.id) {
        $filmsByImdbId[[string]$film.imdb.id] = $i
    }
}

# --- Merge list entries into the database ---

$added = 0
$updated = 0
foreach ($item in $listItems) {
    if ($filmsByImdbId.ContainsKey($item.Id)) {
        # Update existing entry, preserving other imdb fields and other lists.
        $film = $films[$filmsByImdbId[$item.Id]]
        $imdb = $film['imdb']
        if ($item.Title) { $imdb['title'] = $item.Title }
        $updated++
    } else {
        # Add new entry.
        $imdb = [ordered]@{
            id    = $item.Id
            title = $item.Title
        }
        $film = [ordered]@{ imdb = $imdb }
        $films.Add($film)
        $filmsByImdbId[$item.Id] = $films.Count - 1
        $added++
    }

    if (-not $imdb.Contains('lists') -or -not $imdb['lists']) {
        $imdb['lists'] = [ordered]@{}
    }
    $imdb['lists'][$listName] = $item.Rank

    Add-Origin $film 'imdb'
}

# --- Reorder keys and write films.yaml ---

for ($i = 0; $i -lt $films.Count; $i++) {
    $films[$i] = Format-FilmEntry $films[$i]
}
$films | Export-Yaml -Path $DatabasePath

Write-Host "Done: $added added, $updated updated - $($films.Count) total entries in $DatabasePath"
