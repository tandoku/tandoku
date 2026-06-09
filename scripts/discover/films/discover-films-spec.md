# discover films
Tools for compiling a database to aid discovery and ranking of Japanese films (including movies and TV series) for language learning.

# Data
## `films.yaml`
YAML file containing a stream of documets representing films (movie or TV series).

```yaml
wikidata: Q130305455
title: The Fragrant Flower Blooms with Dignity
title-ja: 薫る花は凛と咲く
type:
  - anime-television-series
originCountry:
  - Japan
originalLanguage:
  - ja
year: 2025 # derived from wikidata 'start time'
imdb:
  id: tt36592690
  rating: 8.3
  votes: 12000
myAnimeList:
  id: 59845
tmdb:
  id: 271607
  kind: tv-series
natively:
  language: ja
  level: 21
  url: https://learnnatively.com/tv/66797d747a/
providers:
  netflix:
    id: 82024665
    title: The Fragrant Flower Blooms With Dignity
    watchlist: true
---
wikidata: Q626942
title: Good Luck!!
title-ja: GOOD LUCK!!
type:
  - japanese-television-drama
originCountry:
  - Japan
originalLanguage:
  - ja
year: 2003
imdb:
  id: tt0399971
  rating: 7.5
  votes: 830
tmdb:
  id: 2146
  kind: tv-series
natively:
  language: ja
  level: 30
  temporaryLevel: true
  url: https://learnnatively.com/tv/8c14b5b860/
providers:
  netflix:
    id: 81922646
    title: Good Luck!!
    watchlist: true
```

# Scripts
## ImportNetflixWatchlist.ps1
### Usage
```powershell
ImportNetflixWatchlist.ps1 -Path <netflix-my-list.json> -DatabasePath <films.yaml>
```

### Parameters
- `-Path` - Path to the netflix-my-list.json created by [Netflix Watch List Manager](https://chromewebstore.google.com/detail/netflix-watch-list-manage/obgidigipndchfoaapdbldekffjpmmfa).
- `-DatabasePath` - Path to the films.yaml database file.

### Behavior
Imports Netflix watch list into films.yaml by adding or updating entries matched by `providers.netflix.id`, including the following fields: `providers.netflix.id`, `providers.netflix.title`, and `providers.netflix.watchlist`. Additionally, any items in films.yaml with `providers.netflix.watchlist` set to `true` that are no longer in the imported watch list should be set to `false`.

## PopulateWikidata.ps1
### Usage
```powershell
PopulateWikidata.ps1 -DatabasePath <films.yaml> [-Force]
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.
- `-Force` - When specified, re-runs both phases for every entry even if the data is already present, refreshing existing values and removing fields that Wikidata no longer returns. Without it, the script only fills in missing data.

### Behavior
Iterates over each entry in films.yaml and populates data from Wikidata in two phases:

1. **QID lookup** - For entries missing the `wikidata` field that have `providers.netflix.id`, looks up the Wikidata entity ID associated with the Netflix ID (using Wikidata property P1874). With `-Force`, re-resolves the QID for every entry that has a Netflix ID.
2. **Details enrichment** - For entries that have a `wikidata` QID but are missing a `title`, queries Wikidata to populate additional fields: `title` (English label), `title-ja` (Japanese label), `type` (instance of / P31, kebab-cased, as a list of all values), `originCountry` (country of origin / P495, as a list of all values), `originalLanguage` (original language codes / P364 + P424, falling back to language of work or name / P407 + P424 when P364 is not set, as a list of all values), `year` (start time / P580, or publication date / P577), `imdb.id` (P345), `myAnimeList.id` (P4086), `tmdb.id` (TMDb movie P4947 or TV series P4983), and `tmdb.kind` (`movie` or `tv-series` to disambiguate the TMDb ID). When Wikidata returns multiple IDs for `imdb.id`, `myAnimeList.id`, or `tmdb.id`, the script warns and keeps the lowest value for stability; for `tmdb.id` it prefers a TV series ID over a movie ID (warning also when both movie and TV IDs are present). With `-Force`, enriches every entry that has a QID and overwrites each field, removing any field for which Wikidata no longer returns a value.

## UpdateIMDbData.ps1
### Usage
```powershell
UpdateIMDbData.ps1 -ImdbDataPath <path> -Datasets <names> [-UpdateImdbData]
```

### Parameters
- `-ImdbDataPath` - Path to a local folder for storing IMDb data files downloaded from https://datasets.imdbws.com.
- `-Datasets` - One or more IMDb dataset names to make available, given without the `.tsv.gz` suffix (e.g. `title.ratings`, `title.basics`, `title.akas`).
- `-UpdateImdbData` - When specified, re-downloads (and re-extracts) the data files even if they already exist locally.

### Behavior
Shared helper used by the other IMDb-consuming scripts. For each requested dataset, downloads `<dataset>.tsv.gz` from the IMDb daily data dumps into `-ImdbDataPath` and extracts it to `<dataset>.tsv`. Existing `.tsv` files are reused unless `-UpdateImdbData` is specified; a previously downloaded `.gz` is reused (and only re-extracted) when its `.tsv` is missing. Extraction is written to a temporary file and moved into place so an interrupted run never leaves a partial `.tsv`. Returns an ordered hashtable mapping each dataset name to the full path of its extracted `.tsv` file.

## PopulateIMDb.ps1
### Usage
```powershell
PopulateIMDb.ps1 -DatabasePath <films.yaml> -ImdbDataPath <path> [-UpdateImdbData]
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.
- `-ImdbDataPath` - Path to a local folder for storing IMDb data files downloaded from https://datasets.imdbws.com.
- `-UpdateImdbData` - When specified, re-downloads IMDb data files even if they already exist locally.

### Behavior
Uses `UpdateIMDbData.ps1` to download `title.ratings.tsv.gz` from IMDb daily data dumps and extract it to the folder specified by `-ImdbDataPath` (passing through `-UpdateImdbData`). For each entry in films.yaml that has `imdb.id`, looks up the IMDb rating and vote count and updates the `imdb.rating` and `imdb.votes` fields.

## SuggestWikidataIdentifiers.ps1
### Usage
```powershell
SuggestWikidataIdentifiers.ps1 -DatabasePath <films.yaml> -OutputPath <candidates.yaml> -ImdbDataPath <path> [-UpdateImdbData]
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.
- `-OutputPath` - Path to the YAML file to write proposed candidates to.
- `-ImdbDataPath` - Path to a local folder for storing IMDb data files downloaded from https://datasets.imdbws.com.
- `-UpdateImdbData` - When specified, re-downloads IMDb data files even if they already exist locally.

### Behavior
Proposes candidates for filling Wikidata data gaps. Considers every entry in films.yaml that has `providers.netflix.id` but no `wikidata` field, indexing them by a normalized form of `providers.netflix.title` (lower-cased, runs of non-letter/non-digit characters collapsed to a single space, trimmed); a warning is emitted when several films share the same normalized title.

Uses `UpdateIMDbData.ps1` to fetch `title.akas`, `title.basics`, and `title.ratings`, then makes one streaming pass over each:

1. **akas** - alternate titles are grouped by `titleId` (rows for a title are contiguous in the file); a title is a candidate when any of its alternate titles matches a needed Netflix title. While scanning the group it also records a Japanese-language signal (an alternate title tagged language `ja`, or one containing Hiragana/Katakana/Han characters) - this catches Japanese works whose matched title is the English/romaji one while the Japanese title appears in a separate row.
2. **basics** - additionally matches each title's `primaryTitle`/`originalTitle`, and for every candidate records its `titleType` and whether its `originalTitle` contains Japanese text.
3. **ratings** - records vote counts for candidate titles (used only as a tie-breaker).

Candidate IMDb titles are kept when their `titleType` is a watchable type (`movie`, `tvMovie`, `tvSeries`, `tvMiniSeries`, `short`, `tvShort`, `tvSpecial`, `video`) **and** they carry a Japanese signal (from either the akas or the original title). For each film the best surviving candidate is chosen by preferred title type, then most votes, then lowest IMDb ID; a warning lists the alternatives whenever more than one candidate survives. The chosen IMDb IDs are then looked up on Wikidata in batches (property P345) to find any entity that already exists.

Writes one YAML document per processed film to `-OutputPath`. The `imdb` section is a list of all surviving candidates ordered the same way the best match is chosen (preferred title type, then most votes, then lowest IMDb ID), so the selected candidate is first; the `wikidata` section corresponds to that selected candidate. The `imdb` and/or `wikidata` sections are omitted when no match was found. Each document ends with `verified: false`, a placeholder for a future manual-review workflow:

```yaml
netflix:
  title: <netflix-title>
  id: <netflix-id>
  url: <netflix-title-url>
imdb:
  - title: <imdb-title>
    type: <imdb-title-type>
    year: <imdb-start-year>
    id: <imdb-id>
    url: <imdb-title-url>
  # ...additional candidates in ranked order
wikidata:
  id: <wikidata-id>
  url: <wikidata-entity-url>
verified: false
---
# one document per film in YAML stream
```


## CommitWikidataIdentifiers.ps1
### Usage
```powershell
CommitWikidataIdentifiers.ps1 -CandidatesPath <candidates.yaml> [-AccessToken <token>] [-ApiUrl <url>] [-WhatIf]
```

### Parameters
- `-CandidatesPath` - Path to the candidates YAML stream produced by `SuggestWikidataIdentifiers.ps1` (after manual review).
- `-AccessToken` - Wikidata OAuth 2.0 owner-only access token used to authenticate edits. Falls back to the `WIKIDATA_ACCESS_TOKEN` environment variable. Only required when actually writing (not needed with `-WhatIf`).
- `-ApiUrl` - Wikidata Action API endpoint. Defaults to `https://www.wikidata.org/w/api.php`.
- `-WhatIf` - Previews the claims that would be added without making any edits (and without requiring a token).

### Behavior
Reads the candidates YAML and writes Netflix ID (P1874) and IMDb ID (P345) claims to the corresponding Wikidata entities. Only `netflix.id`, `imdb.id`, and `wikidata.id` are used; all other fields are ignored apart from the `verified` flag.

Each document is processed as follows:
- Documents without `verified: true` are ignored.
- A valid `wikidata.id` (`Q`-number) is required; the record is skipped with a warning otherwise.
- A record with more than one `imdb` entry is skipped with a warning - the reviewer is expected to remove the incorrect entries first. A record with a single `imdb` entry contributes its `imdb.id` (validated as `tt`-number); a record with no `imdb` entry commits only the Netflix ID.
- Existing P345/P1874 claims on the entity are read first (a public, unauthenticated request). If the entity already has an IMDb **or** Netflix ID that does not match the candidate, the whole record is skipped with a warning, since a mismatching identifier means the `wikidata.id` mapping is suspect.
- Only missing claims are added, so re-running the script is idempotent. A record whose identifiers are already present is reported as up to date.

Writes use the Action API (`wbcreateclaim`) with `assert=user` and `maxlag=5` (retrying on replication lag); a CSRF token is fetched lazily only when an edit is actually performed. Per-record failures are reported individually so a partial update (e.g. Netflix written but IMDb failed) is visible and corrected on the next run.


## PopulateNatively.ps1
### Usage
```powershell
PopulateNatively.ps1 -DatabasePath <films.yaml> -NativelyDataPath <path> [-UpdateNativelyData] [-NativelyLanguage <code>] [-OriginalLanguage <codes>]
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.
- `-NativelyDataPath` - Path to a local folder for storing the Natively video catalog downloaded from [Natively](https://learnnatively.com).
- `-UpdateNativelyData` - When specified, re-downloads the Natively catalog even if a local copy already exists.
- `-NativelyLanguage` - Two-letter ISO 639-1 language code for the Natively catalog (defaults to `ja`). Converted internally to the three-letter ISO 639-2 code (e.g. `ja` → `jpn`) used for the Natively API and the local data file names.
- `-OriginalLanguage` - Optional list of two-letter ISO 639-1 codes. When non-empty, only films whose `originalLanguage` includes at least one of these codes are processed.

### Behavior
Pre-fetches the Natively video catalog for the language in two slices - movies (`itype=movie`, `series=all_volumes`, expanding collections into individual movie volumes) and TV series (`itype=tv_season`, `series=series`, grouping seasons under their parent series) - paging through the search API with no `q=` filter and storing them as `natively-movies-<code>.json` and `natively-tv-<code>.json` in `-NativelyDataPath` (where `<code>` is the three-letter ISO 639-2 language code). Uses the existing local files unless they are missing or `-UpdateNativelyData` is specified. Each catalog entry is normalized to a TMDB id, a kind (`movie` or `tv-series`, distinguishing movies from TV series/seasons), difficulty level, and URL, and indexed by `tmdb.id` + `tmdb.kind`. When multiple catalog entries share the same key, the one with a usable level (and a non-season source) is preferred.

For each entry in films.yaml that has `tmdb.id` (and, when `-OriginalLanguage` is supplied, whose `originalLanguage` includes at least one of those codes), matches against the local catalog index by `tmdb.id` and `tmdb.kind` directly. Every eligible entry is (re)looked up on each run, so existing `natively` metadata is refreshed against the current catalog rather than only filling in missing data. If a match with a usable level is found, populates `natively.language` (the `-NativelyLanguage` two-letter code), `natively.level` (difficulty level), `natively.temporaryLevel` (set to `true` when the level is a temporary rating), and `natively.url`. Warns if no matching catalog entry is found. The catalog download is skipped entirely when no entries need a lookup and `-UpdateNativelyData` is not specified.

## ExportFilms.ps1
### Usage
```powershell
ExportFilms.ps1 -DatabasePath <films.yaml> [-OutputPath <films.json>]
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.
- `-OutputPath` - Path to the output JSON file. Defaults to `films.json` in the current directory.

### Behavior
Exports films.yaml to a JSON file with the same structure, wrapping all film records in an outer JSON array.
