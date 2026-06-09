[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath,

    [string]$TitleLanguage = 'ja'
)

Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"

# Ensure UTF-8 encoding for yq compatibility
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8

# Preferred key order for migrated film entries
$fieldOrder = @('wikidata', 'title', 'type', 'country', 'language', 'year', 'imdb', 'myAnimeList', 'tmdb', 'natively', 'availability')

# Old field names mapped to their new names (1:1 renames)
$renames = [ordered]@{
    originCountry    = 'country'
    originalLanguage = 'language'
    providers        = 'availability'
}

function Reorder-FilmEntry($film) {
    $ordered = [ordered]@{}
    foreach ($key in $fieldOrder) {
        if ($film.Contains($key)) {
            $ordered[$key] = $film[$key]
        }
    }
    foreach ($key in @($film.Keys)) {
        if (-not $ordered.Contains($key)) {
            $ordered[$key] = $film[$key]
        }
    }
    return $ordered
}

# Convert a single old-format entry to the new format. Returns the number of
# changes made so callers can report whether anything was migrated.
function Migrate-FilmEntry($film) {
    $changes = 0

    # title (string) + title-ja (string) -> title: { en: ..., <lang>: ... }
    if ($film.title -isnot [System.Collections.IDictionary]) {
        $title = [ordered]@{}
        if ($film.Contains('title') -and $null -ne $film['title']) {
            $title['en'] = [string]$film['title']
        }
        if ($film.Contains('title-ja') -and $null -ne $film['title-ja']) {
            $title[$TitleLanguage] = [string]$film['title-ja']
        }
        $film.Remove('title-ja')
        if ($title.Count -gt 0) {
            $film['title'] = $title
        } else {
            $film.Remove('title')
        }
        $changes++
    }

    # Field renames (originCountry -> country, etc.)
    foreach ($oldKey in $renames.Keys) {
        if ($film.Contains($oldKey)) {
            $newKey = $renames[$oldKey]
            if (-not $film.Contains($newKey)) {
                $film[$newKey] = $film[$oldKey]
            }
            $film.Remove($oldKey)
            $changes++
        }
    }

    return $changes
}

# --- Read films database ---

$films = [System.Collections.Generic.List[object]]::new()
foreach ($doc in @(Import-Yaml -LiteralPath $DatabasePath)) {
    $films.Add($doc)
}

Write-Host "Read $($films.Count) entries from films database"

# --- Migrate each entry ---

$migrated = 0
for ($i = 0; $i -lt $films.Count; $i++) {
    $changes = Migrate-FilmEntry $films[$i]
    if ($changes -gt 0) {
        $migrated++
    }
    $films[$i] = Reorder-FilmEntry $films[$i]
}

$films | Export-Yaml -Path $DatabasePath

Write-Host "Done: migrated $migrated of $($films.Count) entries in $DatabasePath"
