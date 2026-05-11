# Copilot instructions for tandoku

tandoku is a Japanese reading system: a .NET CLI plus a body of PowerShell scripts that
help import, process, and study native Japanese content (books, video, subtitles, Anki).

## Repo layout

| Path       | Contents                                                                 |
|------------|--------------------------------------------------------------------------|
| `src/`     | Production .NET projects (`Tandoku.Core` library, `Tandoku.CommandLine` exe → assembly name `tandoku`) |
| `tests/`   | TUnit test projects mirroring `src/`                                     |
| `scripts/` | PowerShell prototypes of tandoku commands (e.g. `TandokuVolumeNew.ps1`); `scripts/common` holds shared modules dot-sourced or imported via `using` |
| `docs/`    | Workflow definitions and JSON schemas                                    |
| `legacy/`  | Older .NET + PowerShell code, **not** in the active solution (`tandoku.slnx`) — avoid editing unless explicitly asked |
| `resources/` | Static resource files                                                  |

## Build / test / lint

Target framework is **`net10.0`** for all active projects. Tests use **TUnit**
on Microsoft.Testing.Platform; the repo's `global.json` opts `dotnet test` into
the .NET 10 SDK MTP runner, which means projects/solutions must be passed via
`--solution` / `--project` (no positional argument).

```bash
# Build everything in the active solution
dotnet build tandoku.slnx

# Run all tests
dotnet test --solution tandoku.slnx

# Run a single test project
dotnet test --project tests/Tandoku.CommandLine.Tests

# Run a single test by name (TUnit / MTP filter)
dotnet test --project tests/Tandoku.CommandLine.Tests -- --treenode-filter "/*/*/VolumeCommandTests/Init"
```

There is no separate lint step; style is enforced via `.editorconfig` and
`tandoku.ruleset` (referenced from each csproj). Treat editorconfig violations
as part of "the build".

## Architecture

- **`Tandoku.Core`** is a pure library organized by domain folder
  (`Library`, `Volume`, `Content`, `Subtitles`, `Images`, `Packaging`,
  `Markdown`, `Serialization`, `Yaml`, `Common`). Namespaces follow folder
  structure (`dotnet_style_namespace_match_folder = true`).
- **`Tandoku.CommandLine`** is the `tandoku` executable. `Program.cs` is split
  into `Program.<Area>Commands.cs` partial files (Library / Volume / Source /
  Content / Subtitles). Each `CreateXxxCommand()` builds a `System.CommandLine`
  `Command`, defines its options/arguments as locals, and wires behavior via
  `command.SetAction(async (parseResult, ct) => …)` reading values with
  `parseResult.GetValue(option)`. This is the **new System.CommandLine 2.0.x
  API** — do not use the older `SetHandler` / `BinderBase` / `CommandLineBuilder`
  pattern that appears in tutorials and in `legacy/`.
- `Program` takes injected `TextWriter output/error`, `IFileSystem`, and
  `IEnvironment` (see `Abstractions/`) so the whole CLI is testable against an
  in-memory file system. Always route file/console I/O through these, never
  through `Console.*` or `System.IO.File` directly inside command handlers.
- A root-level `--json-output` option (the `jsonOutputOption` field on
  `Program`) is recursive across subcommands. Commands that produce structured
  results check `parseResult.GetValue(this.jsonOutputOption)` and branch
  between human-readable text and `WriteJsonOutput(...)`.

## Test conventions

- **TUnit** + **FluentAssertions** + **Verify.TUnit** (with `Verify.DiffPlex`
  for the CommandLine tests). `global using FluentAssertions; global using
  TUnit.Core;` lives in each test project's `Usings.cs`.
- Use `[Test]` for facts and `[Test]` + `[Arguments(...)]` for parameterized
  tests — never `[Fact]` / `[Theory]` / `[InlineData]` (those are xUnit and the
  packages aren't referenced).
- Don't `using static TUnit.Assertions.Assert` — the tests use FluentAssertions
  (`.Should()`); pulling in TUnit's `Assert` creates name conflicts.
- CLI tests derive from `CliTestBase`, which constructs `Program` against a
  `MockFileSystem` (`System.IO.Abstractions.TestingHelpers`) and string writers.
  Prefer the helpers there:
  - `RunAndAssertAsync(commandLine, expectedOutput?, expectedError?, expectedResult?)` for simple assertions.
  - `RunAndVerifyAsync(commandLine, jsonOutput?)` for snapshot tests. Snapshots
    live under `tests/<project>/Snapshots/` (configured in `ModuleInitialization.cs`).
    The non-JSON branch calls `.IgnoreParameters()` so the snapshot filename has
    no parameter suffix; the JSON branch calls `.UseParameters(jsonOutput)`.
    Verify.TUnit auto-derives parameter suffixes from the test method's
    `[Arguments]`, so omit those calls only when you actually want the suffix.
  - JSON output is round-tripped through YAML before snapshotting so diffs stay readable.
- `ModuleInitialization` adds a scrubber replacing `\` with `/` so snapshots
  are cross-platform; keep new tests platform-neutral (no hard-coded separators).
- To accept new/updated snapshots, install the `verify.tool` global tool or a
  supported diff viewer (see `README.md`).

## Style highlights (from `.editorconfig`)

- **File-scoped namespaces**, and `using` directives go **inside** the namespace
  (`csharp_using_directive_placement = inside_namespace:warning`).
- CRLF line endings, 4-space indent, no final newline on `.cs` files.
- `var` preferred everywhere; predefined types (`int`, `string`) over BCL names.
- `sealed` is used liberally on classes that aren't designed for inheritance
  (e.g. `public sealed partial class Program`); follow suit.
- Parentheses required around binary/relational operators for clarity.

## Working with scripts vs. .NET

The PowerShell scripts in `scripts/` are **prototypes** that often predate the
equivalent .NET command. When porting a script to .NET, the script is the
behavioral spec; keep the script working until the .NET version reaches parity.

## Things to avoid

- Don't add projects to `tandoku.slnx` from `legacy/` — that code is kept for
  reference only and some of it (e.g. `Tandoku.Reader`) targets Windows.
- Don't introduce `Console.WriteLine` / `File.*` directly in command handlers;
  use the injected `output`/`error`/`fileSystem`/`environment` so tests stay hermetic.
