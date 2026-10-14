# Proposed films record format

This is a design example, not the format currently supported by the scripts.
It inventories the fields written to `films.yaml` by the scripts in this folder;
it does not include every field returned by the upstream APIs or stored in caches.
The [current specification](discover-films-spec.md) describes the existing format.

## Structure

- `origin` identifies the import inputs responsible for including a work in the
  database, rather than every service that has enriched it.
- `identifiers` holds shared work identifiers, independent of which source
  discovered them. TMDB's `kind` is part of its identity because movie and TV
  identifiers occupy separate namespaces.
- `sources` holds each input's own metadata. In particular, list-export titles
  are separate from IMDb dataset titles, and watchlist titles are separate from
  uNoGS catalog titles. Wikidata's descriptive fields move here too.

There is no separate `availability` block in this proposal: the only provider
currently written by these scripts is Netflix, whose availability information
belongs to `sources.netflix-unogs.countryDetails`.

## Full example

This deliberately fictional series demonstrates every currently written field.
Identifiers, URLs, ratings, and dates are illustrative, not a verified mapping
between real services. Optional fields are included to show their shape; a
normal record would contain only the identifiers and source data actually known.
Each record remains one YAML document in a stream, separated by `---`.

```yaml
origin:
  - imdb-lists
  - netflix-unogs
  - netflix-watchlist
identifiers:
  wikidata:
    id: Q123456789
  imdb:
    id: tt123456789
  netflix:
    id: 81234567
  myAnimeList:
    id: 12345
  tmdb:
    id: 123456
    kind: tv-series
sources:
  wikidata:
    title:
      en: My Film
      ja: 私の作品
    type:
      - anime-television-series
      - television-series
    country:
      - Japan
    language:
      - ja
    year: 2022
  imdb:
    title: My Film
    originalTitle: Watashi no Sakuhin
    type: tvSeries
    adult: true
    year: 2022
    endYear: 2024
    runtime: 24
    genres:
      - Animation
      - Drama
    seasons: 2
    episodes: 24
    rating: 7.8
    votes: 12000
  imdb-lists:
    title: My Film (TV Series)
    lists:
      queue-japanese: 32
      completed-japanese-intensive-immersion: 12
      unranked-example: null
  tmdb:
    images:
      small: https://image.tmdb.org/t/p/w342/example.jpg
      large: https://image.tmdb.org/t/p/w780/example.jpg
  natively:
    language: ja
    level: 30
    temporaryLevel: true
    url: https://learnnatively.com/tv/example/
  netflix-unogs:
    title: My & Film
    type: series
    year: 2022
    matlabel: TV-MA
    images:
      small: https://example.com/netflix-small.jpg
      large: https://example.com/netflix-large.jpg
    genres:
      - nfid: 7424
        genre: Anime
      - nfid: 5763
        genre: TV Dramas
    countryDetails:
      JP:
        seasonDetails: "S1:12,S2:12"
        newDate: "2022-04-01"
        originalAudio: [ja]
        audio: [ja]
        subtitle: [ja]
      US:
        seasonDetails: "S1:12,S2:12"
        newDate: "2022-07-10"
        expireDate: "2026-12-31"
        originalAudio: [ja]
        audio: [en, ja]
        subtitle: [en, ja]
  netflix-watchlist:
    title: My Film
    watchlist: true
```

## Field schema

The following is a field/type reference, not a machine-enforced JSON Schema.
All source blocks and descriptive fields are optional, since records can be
partially imported or enriched. Each identifier block, when present, needs `id`;
TMDB additionally needs `kind`. Keep lists as arrays even with one element.
Omit unknown fields unless the writer explicitly represents missing data as
`null`. Do not infer an empty source block from the presence of an identifier.

### Origins and identifiers

| Path | Type / values | Current producer |
| --- | --- | --- |
| `origin` | Unique, sorted array of import-input names: `imdb-lists`, `netflix-unogs`, `netflix-watchlist` | The three import scripts; enrichment scripts do not add origins |
| `identifiers.wikidata.id` | String matching `^Q[1-9][0-9]*$` | `PopulateWikidata.ps1`, resolving imported identifiers |
| `identifiers.imdb.id` | String matching `^tt[0-9]+$` | `ImportIMDbList.ps1`; Wikidata P345 |
| `identifiers.netflix.id` | Integer | `ImportNetflixCatalog.ps1`; `ImportNetflixWatchlist.ps1` |
| `identifiers.myAnimeList.id` | Integer | Wikidata P4086 |
| `identifiers.tmdb.id` | Integer | Wikidata P4947/P4983; TMDB lookup by IMDb ID |
| `identifiers.tmdb.kind` | `movie` or `tv-series` | Wikidata property selection; TMDB lookup result kind |

The identifier types above retain the current script output types. Treat numeric
IDs as opaque identifiers, not quantities. Normalizing all IDs to strings would
be a separate design decision.

Wikidata currently **checks** Netflix IDs from P1874 against imported IDs but does
not populate them. Natively currently matches by TMDB ID and kind; it writes a
URL, not a separate Natively ID. Retain that URL in its source block rather than
inventing an identifier extraction rule. There is no MyAnimeList metadata
population script, so there is no `sources.myAnimeList` block yet.

### Source metadata

Paths below are relative to `sources`.

| Path | Type / meaning |
| --- | --- |
| `wikidata.title` | Dictionary of language code to title string; English plus the requested `-Language` label when available |
| `wikidata.type` | Array of strings: English P31 labels lowercased with spaces replaced by hyphens; not a fixed enum |
| `wikidata.country` | Array of English country-name strings from P495, not country codes |
| `wikidata.language` | Array of Wikidata P424 language-code strings: P364 first, P407 fallback; do not assume every returned code has two characters |
| `wikidata.year` | Integer: year of P580, falling back to P577 |
| `imdb.title`, `imdb.originalTitle` | Strings from primary and original titles |
| `imdb.type` | String from IMDb `titleType`, e.g. `movie`, `tvSeries`, `tvMiniSeries`, `short`, `tvMovie`, `tvShort`, `tvSpecial`, `video`, `tvEpisode`, `videoGame`, `tvPilot`; the population script does not filter title types |
| `imdb.adult` | Boolean; the current writer emits only `true`, not `false` |
| `imdb.year`, `imdb.endYear` | Integers from start/end year |
| `imdb.runtime` | Integer, minutes |
| `imdb.genres` | Array of IMDb genre-name strings |
| `imdb.seasons` | Integer: distinct known season numbers in the episode dataset, including season zero if present |
| `imdb.episodes` | Integer: episode rows referencing the series, including rows with unknown season numbers |
| `imdb.rating` | Number from IMDb `averageRating`, on its 1-10 scale |
| `imdb.votes` | Integer from IMDb `numVotes` |
| `imdb-lists.title` | String or null from the CSV `Title`; each import can refresh the single title in this block |
| `imdb-lists.lists` | Dictionary of CSV filename stem to integer `Position`, or null when no position was supplied |
| `tmdb.images.small` | String URL, poster at `w342` |
| `tmdb.images.large` | String URL, poster at `w780` |
| `natively.language` | String: two-letter `-NativelyLanguage` catalog code, not necessarily the work's original language |
| `natively.level` | Integer difficulty level; the current writer only updates a record when a usable level exists |
| `natively.temporaryLevel` | Boolean; the current writer emits only `true`, not `false` |
| `natively.url` | String URL for the matched movie or series |
| `netflix-unogs.title` | String passed through from the uNoGS search result |
| `netflix-unogs.type` | String from uNoGS `vtype`, normally `movie` or `series`; distinct from IMDb type and TMDB kind |
| `netflix-unogs.year` | Upstream year value passed through without conversion; integer or string depending on the API/cache data |
| `netflix-unogs.matlabel` | String maturity label, omitted when absent/empty |
| `netflix-unogs.images.small`, `netflix-unogs.images.large` | String URL or null from uNoGS `img`/`lgimg`; the script builds both keys even when an image is missing |
| `netflix-unogs.genres` | Array of objects with integer `nfid` (a **genre** ID, not the film's Netflix ID) and string `genre` (genre name); omitted when empty |
| `netflix-unogs.countryDetails` | Dictionary keyed by uppercase ISO 3166-1 alpha-2 availability country code |
| `netflix-unogs.countryDetails.<country>.seasonDetails` | Upstream `seasdet` value passed through unchanged, normally a string; omitted when absent/empty, not parsed into episode counts |
| `netflix-unogs.countryDetails.<country>.newDate` | Upstream `newdate` string or null; always assigned for an available country |
| `netflix-unogs.countryDetails.<country>.expireDate` | Upstream `expiredate` string, omitted when absent/empty |
| `netflix-unogs.countryDetails.<country>.originalAudio` | Sorted, unique array of normalized ISO language-code strings for tracks marked `[Original]` |
| `netflix-unogs.countryDetails.<country>.audio` | Sorted, unique array of normalized ISO language-code strings for all audio tracks |
| `netflix-unogs.countryDetails.<country>.subtitle` | Sorted, unique array of normalized ISO language-code strings for subtitles |
| `netflix-watchlist.title` | String or null from the watchlist `title` |
| `netflix-watchlist.watchlist` | Boolean, including explicit `false` when a previously listed work is absent from a later import |

Quote date strings to avoid YAML date coercion. The catalog writer does not
validate or normalize upstream dates; the example's `YYYY-MM-DD` strings do not
imply the scripts enforce that format. Empty audio/subtitle arrays are valid.
Availability countries are distinct from Wikidata's countries of origin.

## Mapping from the current format

| Current path | Proposed path |
| --- | --- |
| `wikidata` | `identifiers.wikidata.id` |
| `title`, `type`, `country`, `language`, `year` | Corresponding fields under `sources.wikidata` |
| `imdb.id` | `identifiers.imdb.id` |
| `imdb.lists` | `sources.imdb-lists.lists` |
| `imdb.title` | `sources.imdb.title` and/or `sources.imdb-lists.title`, depending on provenance |
| Other IMDb metadata | Corresponding fields under `sources.imdb` |
| `myAnimeList.id` | `identifiers.myAnimeList.id` |
| `tmdb.id`, `tmdb.kind` | `identifiers.tmdb.id`, `identifiers.tmdb.kind` |
| `tmdb.images` | `sources.tmdb.images` |
| `natively.*` | `sources.natively.*` |
| `availability.netflix.id` | `identifiers.netflix.id` |
| `availability.netflix.watchlist` | `sources.netflix-watchlist.watchlist` |
| `availability.netflix.title` | `sources.netflix-unogs.title` and/or `sources.netflix-watchlist.title`, depending on provenance |
| Other Netflix catalog metadata | Corresponding fields under `sources.netflix-unogs` |
| `origin: [imdb]` | `origin: [imdb-lists]` when list membership still exists |
| `origin: [netflix]` | One or both Netflix input tags, depending on provenance |

## Design and migration considerations

Shared identifier storage removes the assumption that one origin exclusively
owns an identifier. A future implementation still needs to distinguish trusted
imported identifiers from enrichment suggestions, check disagreement explicitly,
and avoid silently replacing an imported IMDb/Netflix ID with a Wikidata value.
The three-block shape alone does not retain who asserted each identifier; adding
per-identifier provenance would be an additional design choice.

`origin` should remain separate from the keys in `sources`: enrichment alone
does not justify retaining a work after it leaves all importing inputs. Exactly
when an origin is removed needs an explicit policy before implementation.
Currently IMDb list imports remove the IMDb origin when the last list membership
disappears, but Netflix watchlist imports set `watchlist: false` without removing
the Netflix origin, and catalog imports do not prune vanished catalog titles.
The example does not silently change those behaviors.

A lossless migration cannot recover the two historical IMDb titles or the two
historical Netflix titles: the current scripts share and overwrite those fields.
Existing `origin: [netflix]` also does not tell which Netflix importer supplied
the record. Reimporting the original inputs is preferable to claiming provenance
that the old record cannot establish. Similarly, identifiers populated from
Wikidata do not establish that IMDb/TMDB source metadata was fetched.

If a consumer needs one display title, year, or type, it should resolve that
from these source blocks using an explicit precedence rule. This proposal does
not add a second, unsourced canonical copy at the top level.

`DownloadIMDbLists.ps1` and `UpdateIMDbData.ps1` download inputs but write no
film-record fields. Natively's cached `title`, `tmdbId`, `tmdbKind`, `temporary`,
and `source` fields, cache timestamps, and full TMDB API responses are not
additional film-record fields. `SuggestWikidataIdentifiers.ps1` writes a separate
candidate-review YAML stream (including URLs, ranked IMDb candidates, and
`verified`); `CommitWikidataIdentifiers.ps1` consumes that stream and edits
Wikidata, not `films.yaml`. The export, sync, prune, migration, and shared helper
scripts do not introduce additional source metadata.
