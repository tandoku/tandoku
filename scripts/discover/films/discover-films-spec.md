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
  level: 21
  url: https://learnnatively.com/tv/66797d747a/
  tmdbId: 271607
  tmdbKind: tv-series
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
  level: 30
  temporaryLevel: true
  url: https://learnnatively.com/tv/8c14b5b860/
  tmdbId: 2146
  tmdbKind: tv-series
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
2. **Details enrichment** - For entries that have a `wikidata` QID but are missing a `title`, queries Wikidata to populate additional fields: `title` (English label), `title-ja` (Japanese label), `type` (instance of / P31, kebab-cased, as a list of all values), `originCountry` (country of origin / P495, as a list of all values), `originalLanguage` (original language codes / P364 + P424, as a list of all values), `year` (start time / P580, or publication date / P577), `imdb.id` (P345), `myAnimeList.id` (P4086), `tmdb.id` (TMDb movie P4947 or TV series P4983), and `tmdb.kind` (`movie` or `tv-series` to disambiguate the TMDb ID). When Wikidata returns multiple IDs for `imdb.id`, `myAnimeList.id`, or `tmdb.id`, the script warns and keeps the lowest value for stability; for `tmdb.id` it prefers a TV series ID over a movie ID (warning also when both movie and TV IDs are present). With `-Force`, enriches every entry that has a QID and overwrites each field, removing any field for which Wikidata no longer returns a value.

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
Downloads `title.ratings.tsv.gz` from IMDb daily data dumps and extracts it to the folder specified by `-ImdbDataPath`. Uses existing data files at that path unless `-UpdateImdbData` is specified. For each entry in films.yaml that has `imdb.id`, looks up the IMDb rating and vote count and updates the `imdb.rating` and `imdb.votes` fields.

## PopulateNatively.ps1
### Usage
```powershell
PopulateNatively.ps1 -DatabasePath <films.yaml> -NativelyDataPath <path> [-UpdateNativelyData] [-Language <code>]
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.
- `-NativelyDataPath` - Path to a local folder for storing the Natively video catalog downloaded from [Natively](https://learnnatively.com).
- `-UpdateNativelyData` - When specified, re-downloads the Natively catalog even if a local copy already exists.
- `-Language` - Natively language code for the catalog (defaults to `jpn`). Determines the API language and the local file name `natively-videos-<lang>.json`.

### Behavior
Pre-fetches the entire Natively video catalog for the language (paging through the search API with no `q=` filter) and stores it as `natively-videos-<lang>.json` in `-NativelyDataPath`. Uses the existing local file unless it is missing or `-UpdateNativelyData` is specified. Each catalog entry is normalized to a TMDB id, a kind (`movie` or `tv-series`, distinguishing movies from TV series/seasons), difficulty level, and URL.

For each entry in films.yaml that has `tmdb.id`, matches against the local catalog by `tmdb.id` and `tmdb.kind` directly. An entry is matched when it is missing `natively`, or when the captured `natively.tmdbId`/`natively.tmdbKind` are missing or no longer match the entry's current `tmdb.id`/`tmdb.kind` (so the Natively match is rechecked whenever the TMDB info changes). If a match is found, populates `natively.level` (difficulty level), `natively.url`, `natively.temporaryLevel` (set to `true` when the level is a temporary rating), and records `natively.tmdbId` and `natively.tmdbKind` (the TMDB info that was matched). Warns if no matching catalog entry is found. The catalog download is skipped entirely when no entries need a lookup and `-UpdateNativelyData` is not specified.

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
