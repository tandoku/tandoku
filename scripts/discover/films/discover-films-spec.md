# discover films
Tools for compiling a database to aid discovery and ranking of Japanese films (including movies and TV series) for language learning.

# Data
## `films.yaml`
YAML file containing a stream of documets representing films (movie or TV series).

```yaml
wikidata: Q130305455
title:
  en: The Fragrant Flower Blooms with Dignity
  ja: 薫る花は凛と咲く
type:
  - anime-television-series
country:
  - Japan
language:
  - ja
year: 2025 # derived from wikidata 'start time'
imdb:
  id: tt36592690
  rating: 8.3
  votes: 12000
  lists:
    queue-japanese: 3
    completed-japanese-intensive-immersion: 12
myAnimeList:
  id: 59845
tmdb:
  id: 271607
  kind: tv-series
natively:
  language: ja
  level: 21
  url: https://learnnatively.com/tv/66797d747a/
availability:
  netflix:
    id: 82024665
    title: The Fragrant Flower Blooms With Dignity
    watchlist: true
---
wikidata: Q626942
title:
  en: Good Luck!!
  ja: GOOD LUCK!!
type:
  - japanese-television-drama
country:
  - Japan
language:
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
availability:
  netflix:
    id: 81922646
    title: Good Luck!!
    watchlist: true
```

# Scripts
The scripts in this folder share helpers through the `tandoku-discover-films.psm1`
module (e.g. `Read-FilmsDatabase`, `Format-FilmEntry`, `Add-Origin`). Functions
used by more than one script belong in this module rather than being copied into
each script.

## ImportNetflixWatchlist.ps1
### Usage
```powershell
ImportNetflixWatchlist.ps1 -Path <netflix-my-list.json> -DatabasePath <films.yaml>
```

### Parameters
- `-Path` - Path to the netflix-my-list.json created by [Netflix Watch List Manager](https://chromewebstore.google.com/detail/netflix-watch-list-manage/obgidigipndchfoaapdbldekffjpmmfa).
- `-DatabasePath` - Path to the films.yaml database file.

### Behavior
Imports Netflix watch list into films.yaml by adding or updating entries matched by `availability.netflix.id`, including the following fields: `availability.netflix.id`, `availability.netflix.title`, and `availability.netflix.watchlist`. Additionally, any items in films.yaml with `availability.netflix.watchlist` set to `true` that are no longer in the imported watch list should be set to `false`.

## ImportNetflixCatalog.ps1
### Usage
```powershell
ImportNetflixCatalog.ps1 -DatabasePath <films.yaml> [-Country <codes>] [-AudioLanguage <code>] [-SubtitleLanguage <code>] [-CachePath <dir>] [-RequestLimit <n>] [-ApiKey <key>]
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.
- `-Country` - One or more ISO 3166-1 alpha-2 country codes (defaults to `US`). Titles available in at least one of these countries are imported, and per-country availability details are retrieved for each of them.
- `-AudioLanguage` - Two-letter ISO 639-1 audio language code to filter on (defaults to `ja`). Pass an empty string to skip the audio filter.
- `-SubtitleLanguage` - Two-letter ISO 639-1 subtitle language code to filter on (no default). When both `-AudioLanguage` and `-SubtitleLanguage` are specified, titles must match both (AND filter).
- `-CachePath` - Optional folder for caching uNoGS responses across runs. The Countries response is cached to `netflix-catalog-countries.json`, and Title Countries responses to `netflix-catalog-titlecountries.json` (a Netflix-id-keyed map, sorted by id, where each entry has `results` and a `timestamp`). Cached lookups do not count against `-RequestLimit`.
- `-RequestLimit` - Maximum number of requests sent to the uNoGS API across all endpoints in a single run (defaults to `100`; `0` means no limit). Once the limit is reached the script emits a single warning and skips all remaining requests, leaving the rest for a later run. Combined with `-CachePath`, a large catalog can be imported incrementally across multiple runs.
- `-ApiKey` - uNoGS RapidAPI key. Falls back to the `RAPIDAPI_KEY` environment variable.

### Behavior
Queries the [uNoGS API](https://rapidapi.com/unogs/api/unogsng) for Netflix titles and imports them into films.yaml by adding or updating entries matched by `availability.netflix.id`. Uses the Search endpoint (resolving country codes to uNoGS country IDs via the Countries endpoint) to find titles available in the requested countries with the requested audio/subtitle languages, paging through all results. For each matching title it calls the Title Countries endpoint to retrieve per-country availability details.

Populates `availability.netflix` with `id`, `title`, `type` (`movie` or `series`), `year`, and a `countryDetails` dictionary keyed by country code, each holding `seasonDetails` (omitted when empty), `newDate`, `expireDate` (omitted when not set), and `audio`/`subtitle` lists of ISO 639 language codes (Netflix language names are normalized, deduplicated, and sorted). Existing entries are updated in place, preserving other fields such as `watchlist`, and `netflix` is added to the entry's `origin` list.

API usage is capped by `-RequestLimit`; uncached lookups are skipped once it is reached (titles without availability details are left untouched for a later run). With `-CachePath`, Countries and Title Countries responses are cached so successive runs make fewer requests and can resume where a previous run stopped, making it practical to import a large catalog incrementally.

```yaml
availability:
  netflix:
    id: 81249833
    title: VINLAND SAGA
    type: series
    year: 2019
    countryDetails:
      US:
        seasonDetails: "S1:24,S2:24"
        newDate: 2022-07-10
        audio: [de, en, es, fr, ja]
        subtitle: [en, es, ja, zh]
origin: [netflix]
```

## DownloadIMDbLists.ps1
### Usage
```powershell
DownloadIMDbLists.ps1 -IMDbExportsPath <imdb-exports.html> -CsvPath <dir>
```

### Parameters
- `-IMDbExportsPath` - Path to a saved IMDb exports page (`https://www.imdb.com/exports/`). IMDb only serves this page behind a JavaScript/anti-bot challenge, so it must be saved from a logged-in browser session rather than fetched directly.
- `-CsvPath` - Path to a directory the downloaded list CSV files are written to (created if it does not exist).

### Behavior
Parses the saved exports page's embedded `__NEXT_DATA__` JSON to find every ready title-list export (`exportType` `LIST`, `listType` `TITLES`, `status` `READY`), keeping the most recent export per list id. For each list it downloads the export's CSV from the presigned download URL embedded in the page and saves it as `<kebab-cased list name>.csv` under `-CsvPath`, so the file name doubles as the list name when imported by `ImportIMDbList.ps1`.

Lists are processed in a stable order (sorted by slug, then list id) so that when two distinct list names reduce to the same kebab-cased slug, the collision suffixes (`<slug>.csv`, `<slug>-2.csv`, ...) are assigned deterministically across runs; a warning is emitted for each collision.

> **Note:** the presigned CSV download URLs embedded in the exports page expire a few minutes after the page is loaded. Save the exports page and run this script promptly; if the downloads fail with an expired/forbidden error, re-save the page and try again.

## ImportIMDbList.ps1
### Usage
```powershell
ImportIMDbList.ps1 -DatabasePath <films.yaml> -CsvPath <paths>
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.
- `-CsvPath` - One or more paths to import list CSVs from. Each element may be a directory (all `*.csv` files within it are used), a single CSV file, or a wildcard pattern (e.g. `lists-*.csv`). Resolved files are deduplicated.

### Behavior
Imports one or more IMDb list CSV exports (as produced by `DownloadIMDbLists.ps1`) into films.yaml. Each CSV's file name (without the `.csv` extension) is used as the list name recorded under `imdb.lists.<list-name>: <rank>`, where the rank comes from the CSV `Position` column and the IMDb ID from the `Const` column. Entries are matched by `imdb.id` (creating a new entry when none exists), `imdb` (the title) is populated/refreshed, and `imdb` is added to the entry's `origin` list.

Repeated runs merge without smashing other lists or fields: only the importing list's entry under `imdb.lists` is set, leaving any other lists and `imdb` fields intact. A warning is emitted when two distinct CSV files resolve to the same list name (their `imdb.lists` ranks would otherwise overwrite each other).

Each imported list is treated as authoritative for its own membership: a title previously recorded under a list name that is absent from that list's CSV has its `imdb.lists.<list-name>` entry removed. When this empties a film's `imdb.lists`, the `lists` key is dropped and the `imdb` origin tag is removed (the `imdb.id`/`imdb.title` cross-reference fields are kept). Only lists actually imported in the run are pruned; lists not present in the run are left untouched.

## PopulateWikidata.ps1
### Usage
```powershell
PopulateWikidata.ps1 -DatabasePath <films.yaml> [-Language <code>] [-Force]
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.
- `-Language` - Two-letter ISO 639-1 language code for the localized title fetched alongside the English title (defaults to `ja`). The `title` field is populated as a per-language dictionary (`title.en` plus `title.<Language>`).
- `-Force` - When specified, re-runs both phases for every entry even if the data is already present, refreshing existing values and removing fields that Wikidata no longer returns. Without it, the script only fills in missing data.

### Behavior
Iterates over each entry in films.yaml and populates data from Wikidata. Lookup is **origin-aware**: each origin (e.g. `netflix`, `imdb`) owns exactly one external identifier that is also recorded on Wikidata (Netflix ID → P1874, IMDb ID → P345). An origin-owned identifier is authoritative and is never silently overwritten from Wikidata.

1. **QID lookup** - For entries missing the `wikidata` field, resolves the Wikidata entity ID from each identifier the entry owns (looking it up under that identifier's Wikidata property). All of an entry's owned identifiers must agree: if they resolve to different entities, the script emits an error and leaves `wikidata` unset. With `-Force`, re-resolves the QID for every entry that owns at least one identifier.
2. **Merge duplicates** - Different origins can create separate entries for the same work (e.g. a Netflix import and an IMDb list import). Once both resolve to the same QID, the entries are merged into one: `origin` lists and `imdb.lists` are unioned and any field present on only one entry is carried over. If the entries carry conflicting values for an owned identifier (e.g. different Netflix or IMDb IDs), the merge is skipped and an error is emitted so they can be resolved manually.
3. **Details enrichment** - For entries that have a `wikidata` QID but are missing a `title`, queries Wikidata to populate additional fields: `title` (a per-language dictionary with the English label as `title.en` and the `-Language` label as `title.<Language>`), `type` (instance of / P31, kebab-cased, as a list of all values), `country` (country of origin / P495, as a list of all values), `language` (original language codes / P364 + P424, falling back to language of work or name / P407 + P424 when P364 is not set, as a list of all values), `year` (start time / P580, or publication date / P577), `imdb.id` (P345), `myAnimeList.id` (P4086), `tmdb.id` (TMDb movie P4947 or TV series P4983), and `tmdb.kind` (`movie` or `tv-series` to disambiguate the TMDb ID). When Wikidata returns multiple IDs for `imdb.id`, `myAnimeList.id`, or `tmdb.id`, the script warns and keeps the lowest value for stability; for `tmdb.id` it prefers a TV series ID over a movie ID (warning also when both movie and TV IDs are present). Owned identifiers are only **verified** here, never written: if the entry owns its IMDb (or Netflix) ID and Wikidata reports a different value, the script emits an error and leaves the entry's value untouched. A non-owned `imdb.id` (e.g. on a Netflix-only entry) is still filled in as a cross-reference, preserving any existing `imdb.lists`. With `-Force`, enriches every entry that has a QID and overwrites each non-owned field, removing any field for which Wikidata no longer returns a value.

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
Proposes candidates for filling Wikidata data gaps. Considers every entry in films.yaml that has `availability.netflix.id` but no `wikidata` field, indexing them by a normalized form of `availability.netflix.title` (lower-cased, runs of non-letter/non-digit characters collapsed to a single space, trimmed); a warning is emitted when several films share the same normalized title.

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
CommitWikidataIdentifiers.ps1 -CandidatesPath <candidates.yaml> [-AccessToken <token>] [-ApiUrl <url>] [-Prune] [-WhatIf]
```

### Parameters
- `-CandidatesPath` - Path to the candidates YAML stream produced by `SuggestWikidataIdentifiers.ps1` (after manual review).
- `-AccessToken` - Wikidata OAuth 2.0 owner-only access token used to authenticate edits. Falls back to the `WIKIDATA_ACCESS_TOKEN` environment variable. Only required when actually writing (not needed with `-WhatIf`).
- `-ApiUrl` - Wikidata Action API endpoint. Defaults to `https://www.wikidata.org/w/api.php`.
- `-Prune` - After processing, rewrites the candidates file with every entry that was already up to date (the target entity already had both identifiers) removed, leaving only records that still need attention. Honors `-WhatIf` (no file is written when previewing).
- `-WhatIf` - Previews the claims that would be added without making any edits (and without requiring a token).

### Behavior
Reads the candidates YAML and writes Netflix ID (P1874) and IMDb ID (P345) claims to the corresponding Wikidata entities. Only `netflix.id`, `imdb.id`, and `wikidata.id` are used; all other fields are ignored apart from the `verified` flag.

Each document is processed as follows:
- Documents without `verified: true` are ignored.
- The target entity is identified by `wikidata.id` (a `Q`-number). When a verified record has no `wikidata.id`, the script searches Wikidata for the entity carrying the record's `imdb.id` (P345) and uses that entity; the record is skipped with a warning if there is no `imdb.id` to search by, no matching entity is found, or the IMDb ID maps to more than one entity. A resolved or supplied `wikidata.id` that is not a valid `Q`-number is skipped with a warning.
- A record with more than one `imdb` entry is skipped with a warning - the reviewer is expected to remove the incorrect entries first. A record with a single `imdb` entry contributes its `imdb.id` (validated as `tt`-number); a record with no `imdb` entry commits only the Netflix ID.
- Existing P345/P1874 claims on the entity are read first (a public, unauthenticated request). If the entity already has an IMDb **or** Netflix ID that does not match the candidate, the whole record is skipped with a warning, since a mismatching identifier means the `wikidata.id` mapping is suspect.
- Only missing claims are added, so re-running the script is idempotent. A record whose identifiers are already present is reported as up to date. When `-Prune` is specified, these already-up-to-date entries are removed from the candidates file at the end of the run (skipped, non-verified, and still-pending records are preserved).

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
- `-OriginalLanguage` - Optional list of two-letter ISO 639-1 codes. When non-empty, only films whose `language` includes at least one of these codes are processed; a warning is emitted for any eligible film that has no `language` at all.

### Behavior
Pre-fetches the Natively video catalog for the language in two slices - movies (`itype=movie`, `series=all_volumes`, expanding collections into individual movie volumes) and TV series (`itype=tv_season`, `series=series`, grouping seasons under their parent series) - paging through the search API with no `q=` filter and storing them as `natively-movies-<code>.json` and `natively-tv-<code>.json` in `-NativelyDataPath` (where `<code>` is the three-letter ISO 639-2 language code). Uses the existing local files unless they are missing or `-UpdateNativelyData` is specified. Each catalog entry is normalized to a TMDB id, a kind (`movie` or `tv-series`, distinguishing movies from TV series/seasons), difficulty level, and URL, and indexed by `tmdb.id` + `tmdb.kind`. When multiple catalog entries share the same key, the one with a usable level (and a non-season source) is preferred.

For each entry in films.yaml that has `tmdb.id` (and, when `-OriginalLanguage` is supplied, whose `language` includes at least one of those codes), matches against the local catalog index by `tmdb.id` and `tmdb.kind` directly. Every eligible entry is (re)looked up on each run, so existing `natively` metadata is refreshed against the current catalog rather than only filling in missing data. If a match with a usable level is found, populates `natively.language` (the `-NativelyLanguage` two-letter code), `natively.level` (difficulty level), `natively.temporaryLevel` (set to `true` when the level is a temporary rating), and `natively.url`. Warns if no matching catalog entry is found. The catalog download is skipped entirely when no entries need a lookup and `-UpdateNativelyData` is not specified.

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

## MigrateFilmsDatabase.ps1
### Usage
```powershell
MigrateFilmsDatabase.ps1 -DatabasePath <films.yaml> [-TitleLanguage <code>]
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file to migrate in place.
- `-TitleLanguage` - Two-letter ISO 639-1 language code used as the key for the old `title-ja` value in the new per-language `title` dictionary (defaults to `ja`).

### Behavior
Migrates an existing films.yaml from the legacy format to the current one, rewriting the file in place. For each entry it converts the scalar `title` / `title-ja` fields into a per-language `title` dictionary (`title.en` from `title`, `title.<TitleLanguage>` from `title-ja`) and renames `originCountry` → `country`, `originalLanguage` → `language`, and `providers` → `availability`. Entry keys are reordered to the canonical field order. The migration is idempotent: entries already in the new format are left unchanged.

