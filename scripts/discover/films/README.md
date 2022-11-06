# Rapid API for uNoGS Netflix data

API documentation: https://rapidapi.com/unogs/api/unogs/

Get API Key from https://rapidapi.com/developer/security/unogs-v1-app

To update CSV files:
```
$env:RAPID_API_KEY = '<key>'
.\DownloadNetflixTitles.ps1
```

To query:
```
. .\NetflixQueryTools.ps1
fn <title>
```

# Netflix My List export

Chrome extension: https://chrome.google.com/webstore/detail/netflix-watch-list-manage/obgidigipndchfoaapdbldekffjpmmfa

# Querying with duckdb
## Prereq
```
scoop install duckdb
```

## Loading tables
Note that .gz files are also natively supported.

TODO: duckdb supports JSON lines as well (https://duckdb.org/docs/extensions/json) so consider using that instead of CSV.

```
PRAGMA default_collation='NOCASE.NOACCENT';

CREATE TABLE title_akas AS SELECT * FROM read_csv_auto('.\imdb\title.akas.tsv', ALL_VARCHAR=TRUE, QUOTE=NULL);

CREATE TABLE jpdb_list AS SELECT * FROM read_csv_auto('.\jpdb\anime-difficulty-list.csv');

CREATE TABLE netflix_ja_titles AS SELECT * FROM read_csv_auto('.\netflix-ja-titles.csv');
```

## jpdb-imdb-netflix join
```
select j.title,j.difficulty,a.titleId,n.netflixid,n.audio,n.subtitles
from jpdb_list AS j
    left outer join title_akas AS a ON j.title = a.title
    left outer join (
        select netflixid,n.title,imdbid,a.titleId,audio,subtitles
        from netflix_ja_titles n
            left outer join title_akas a on n.title=a.title
        group by *
    ) n ON a.titleId = n.imdbId OR a.titleId = n.titleId
where j.difficulty='3/10' and n.subtitles='ja'
group by *
order by subtitles desc, audio desc, netflixid desc;
```

# TODOs
- IMDb titles could be limited to just JP/US/XWW rows, see sample below - also include only titles that have a JP/ja title
- IMDb romanization may not match jpdb (e.g. Toki o vs Toki wo), normalize
- IMDb titles may be episodes (join with main titles table to filter these out)
- Normalize case and strip accents so duckdb default binary collation can be used (significantly faster) - do this in PowerShell if needed on duckdb export as part of below script
- Write a script to create a filtered/condensed version of IMDb titles for matching purpose
- Try out duckdb full text index for title matching
- Matching could be improved by including release year but need to scrape this from MyAnimeList
## Fixed
- duckdb is using case-sensitive matching
- IMDb titles can have diacritics (e.g. Toki o kakeru shôjo)

# Sample rows from title.akas.tsv
```
titleId   ordering title                                          region language types       attributes                  isOrigina
                                                                                                                          lTitle
-------   -------- -----                                          ------ -------- -----       ----------                  ---------
tt1568921 12       Kari-gurashi                                   JP     \N       \N          promotional abbreviation    0
tt1568921 19       The Secret World of Arrietty                   JP     en       imdbDisplay \N                          0
tt1568921 20       借りぐらしのアリエッティ                       JP     ja       imdbDisplay \N                          0
tt1568921 26       Yukashita no kobito-tachi                      JP     \N       working     \N                          0
tt1568921 31       Karigurashi no Arietty                         \N     \N       original    \N                          1
tt1568921 38       The Secret World of Arrietty                   US     \N       imdbDisplay \N                          0
tt1568921 43       The Secret World of Arrietty                   XWW    en       imdbDisplay \N                          0
tt1568921 47       Arrietty                                       US     \N       \N          short title                 0
tt1568921 51       Chiisana Ariettei                              JP     \N       working     \N                          0
tt1568921 55       The Borrower Arrietty                          XWW    en       \N          literal English title       0
tt1568921 59       The Secret World of Arrietty                   XAS    en       imdbDisplay \N                          0
tt1568921 62       Kari-gurashi no Arrietti                       JP     \N       \N          alternative transliteration 0
tt1568921 9        Chiisana Arrietty                              JP     \N       working     \N                          0
```
