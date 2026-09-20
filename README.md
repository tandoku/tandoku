# tandoku
tandoku is a Japanese reading system comprising apps and utilities to help read and learn from native Japanese content.

## Repo organization
| Path           | Description                                                                               |
|----------------|-------------------------------------------------------------------------------------------|
| docs           | Documentation, includes workflow definitions                                              |
| scripts        | Scripts implementing prototypes of tandoku commands, file names like TandokuVolumeNew.ps1 |
| scripts/common | Modules and scripts included in other top-level scripts via using or dot sourcing         |
| src            | Source code for production tandoku libraries and tools                                    |
| tests          | Test code for tandoku libraries and tools                                                 |

## Development

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (all active projects target `net10.0`).

### Build
```bash
dotnet build tandoku.slnx
```

### Run the CLI
```bash
dotnet run --project src/Tandoku.CommandLine -- <args>
```
The output assembly name is `tandoku`, so once built you can also invoke
`src/Tandoku.CommandLine/bin/Debug/net10.0/tandoku <args>` directly.

### Run the film browser
```bash
dotnet build src/Tandoku.App/Tandoku.App.csproj -t:Run
```

The app prompts for the multi-document `films.yaml` database produced by the
scripts under `scripts/discover/films`:

```yaml
wikidata: Q12345
title:
  en: Example Film
  ja: サンプル映画
type:
- film
country:
- Japan
language:
- ja
year: 2024
imdb:
  id: tt1234567
  type: movie
  runtime: 112
  genres:
  - Drama
  rating: 7.8
  votes: 12000
  lists:
    japanese: 10
tmdb:
  id: 12345
  kind: movie
  images:
    small: https://image.tmdb.org/t/p/w342/example.jpg
    large: https://image.tmdb.org/t/p/w780/example.jpg
natively:
  language: ja
  level: 24
  url: https://learnnatively.com/movies/example/
origin:
- imdb
---
# next film document
```

### Testing
Tests use [TUnit](https://github.com/thomhurst/TUnit) on Microsoft.Testing.Platform.
The repo's `global.json` opts `dotnet test` into the .NET 10 SDK MTP runner, so
solutions and projects are passed with `--solution` / `--project` rather than as
positional arguments:

```bash
# Run every test
dotnet test --solution tandoku.slnx

# Run one test project
dotnet test --project tests/Tandoku.CommandLine.Tests

# Run a single test (TUnit / MTP filter syntax)
dotnet test --project tests/Tandoku.CommandLine.Tests -- --treenode-filter "/*/*/VolumeCommandTests/Init"
```

Some of the tests leverage the [Verify](https://github.com/VerifyTests/Verify) snapshot tool. For the best experience
updating test snapshots, install a supported diff tool and/or the dotnet global tool [verify.tool](https://github.com/VerifyTests/Verify.Terminal).