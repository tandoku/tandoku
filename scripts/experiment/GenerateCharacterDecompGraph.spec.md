# Character decomposition graph generator
GenerateCharacterDecompGraph.ps1 script generates kanji character decomposition graphs in Mermaid format.

## Usage
```powershell
GenerateCharacterDecompGraph.ps1 -Character <string> -Source uchisen [-Path <output-path>]
```

## Parameters
- `-Character` — A string containing one or more kanji characters. Non-kanji characters (punctuation, numbers, Latin letters, kana, etc.) are ignored. A graph is generated for each kanji character in the string.
- `-Source` — The decomposition source to use. Currently only "uchisen" is supported.
- `-Path` — Optional output path. If omitted, graphs are written to standard output.

## Behavior
When `-Path` is specified, behavior varies by path type:
- If <output-path> refers to a directory, a "<kanji>-<source>.mermaid" file is created in the directory for each character
- If <output-path> refers to a Mermaid file (.mermaid), graphs are written to this file (separated by blank lines for multiple characters)
- If <output-path> refers to a Markdown file (.md), each graph is appended with a preceding newline and surrounding ```mermaid code block

When `-Path` is omitted, graphs are written to standard output (separated by blank lines for multiple characters).

The only supported source right now is "uchisen". The script should look up the specified kanji character on the uchisen website and recursively extract the decomposition of the character to primes and compound kanji components.

The graph should follow the format below.

## Example graph for kanji character `知`
```mermaid
graph LR
    Uchisen(Uchisen) --> Know
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
