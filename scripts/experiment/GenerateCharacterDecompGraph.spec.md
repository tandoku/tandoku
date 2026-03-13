# Character decomposition graph generator
GenerateCharacterDecompGraph.ps1 script generates kanji character decomposition graphs in Mermaid format.

## Usage
```powershell
GenerateCharacterDecompGraph.ps1 -Character <string> [-Source <source[]>] [-WaniKaniApiToken <token>] [-Path <output-path>]
```

## Parameters
- `-Character` — A string containing one or more kanji characters. Non-kanji characters (punctuation, numbers, Latin letters, kana, etc.) are ignored. A graph is generated for each kanji character in the string.
- `-Source` — Optional. One or more decomposition sources: "uchisen", "wanikani", "jpdb". If omitted, all sources are used. Each character is processed for each source (outer loop is characters, inner loop is sources).
- `-Path` — Optional output path. If omitted, graphs are written to standard output.
- `-WaniKaniApiToken` — API token for WaniKani. If not provided, falls back to the `WANIKANI_API_TOKEN` environment variable. Required when wanikani is included in `-Source` (or when `-Source` is omitted).

## Behavior
When `-Path` is specified, behavior varies by path type:
- If <output-path> refers to a directory, a "<kanji>-<source>.mermaid" file is created for each character/source combination
- If <output-path> refers to a Mermaid file (.mermaid), graphs are written to this file (separated by blank lines for multiple graphs)
- If <output-path> refers to a Markdown file (.md), each graph is appended with a preceding newline and surrounding ```mermaid code block. When multiple sources are used, a `# <character>` heading is inserted before the graphs for each character.

When `-Path` is omitted, graphs are written to standard output (separated by blank lines for multiple graphs).

## Sources

### Uchisen
Looks up kanji on uchisen.com and recursively extracts the decomposition into primes and compound kanji components. Primes use diamond `{}` shape and kanji use rectangle `[]` shape.

### WaniKani
Uses the WaniKani API (v2) to look up kanji and extract their radical components. Decomposition is single-level only (kanji → radicals, no recursion). Radicals use diamond `{}` shape and kanji use rectangle `[]` shape. If a radical has the same name as the root kanji (self-decomposition), it is skipped.

### jpdb
Looks up kanji on jpdb.io and recursively extracts the decomposition into components. Characters in the standard CJK Unified Ideographs range are treated as kanji (rectangle `[]` shape) and recursed into; characters outside that range (e.g., CJK Extension B) are treated as primes (diamond `{}` shape). All component URLs use the `/kanji/` path on jpdb.io. Names are lowercase as provided by jpdb.

## Prime Unicode characters

The `uchisen-primes.yaml` file (in the same directory as the script) provides Unicode characters for uchisen primes, which are displayed as SVG images on the website and cannot be scraped directly. The file uses simple `key: value` format:

```yaml
roundhouse kick: 𠂉
```

When the script runs, it loads the file and uses any mapped characters in the graph output. If a prime is encountered at runtime that is not already in the file, a new entry is added with a blank value. The updated file is written back after each run, so the user can fill in missing values offline and re-run the script.

The graph should follow the format below.

## Example graph for kanji character `知`

### Uchisen
```mermaid
---
title: 知 Know - Uchisen
---
graph LR
    Know --> Arrow
    Know --> Mouth
    Arrow --> roundhouse_kick
    Arrow --> Large
    Large --> One
    Large --> Person
    
    Know[知<br/>Know]
    click Know "https://uchisen.com/kanji/%E7%9F%A5"
    
    Arrow[矢<br/>Arrow]
    click Arrow "https://uchisen.com/kanji/%E7%9F%A2"
    
    Mouth[口<br/>Mouth, Entrance]
    click Mouth "https://uchisen.com/kanji/%E5%8F%A3"
    
    roundhouse_kick{𠂉<br/>roundhouse kick}
    click roundhouse_kick "https://uchisen.com/primes/roundhouse+kick"
    
    Large[大<br/>Large]
    click Large "https://uchisen.com/kanji/%E5%A4%A7"
    
    One[一<br/>One]
    click One "https://uchisen.com/kanji/%E4%B8%80"
    
    Person[人<br/>Person]
    click Person "https://uchisen.com/kanji/%E4%BA%BA"
```

### WaniKani
```mermaid
---
title: 知 Know - WaniKani
---
graph LR
    Know --> Arrow
    Know --> Mouth
    
    Know[知<br/>Know]
    click Know "https://www.wanikani.com/kanji/%E7%9F%A5"
    
    Arrow{矢<br/>Arrow}
    click Arrow "https://www.wanikani.com/radicals/arrow"
    
    Mouth{口<br/>Mouth}
    click Mouth "https://www.wanikani.com/radicals/mouth"
```

### jpdb
```mermaid
---
title: 知 know - jpdb.io
---
graph LR
    know --> arrow
    know --> mouth
    arrow --> cow_head
    arrow --> large
    large --> one
    large --> person
    
    know[知<br/>know]
    click know "https://jpdb.io/kanji/%E7%9F%A5"
    
    arrow[矢<br/>arrow]
    click arrow "https://jpdb.io/kanji/%E7%9F%A2"
    
    mouth[口<br/>mouth]
    click mouth "https://jpdb.io/kanji/%E5%8F%A3"
    
    cow_head{𠂉<br/>cow head}
    click cow_head "https://jpdb.io/kanji/%F0%A0%82%89"
    
    large[大<br/>large]
    click large "https://jpdb.io/kanji/%E5%A4%A7"
    
    one[一<br/>one]
    click one "https://jpdb.io/kanji/%E4%B8%80"
    
    person[人<br/>person]
    click person "https://jpdb.io/kanji/%E4%BA%BA"
```
