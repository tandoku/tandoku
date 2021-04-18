$TandokuRoot = 'O:\Tandoku'
$TandokuTools = "$TandokuRoot\Tools"

$TandokuScripts = (Split-Path $MyInvocation.MyCommand.Path -Parent)
$TandokuModules = "$TandokuScripts\tandoku*.psm1"

#Consider adding the scripts directory to $env:PSModulePath rather than loading all modules upfront
Get-ChildItem $TandokuModules |
    Import-Module 

function films { cd $TandokuRoot\Films }
function manga { cd $TandokuRoot\Manga }
function novels { cd $TandokuRoot\Novels }
function pbooks { cd "$TandokuRoot\Picture Books" }
function scripts { cd $TandokuScripts }
function tools { cd $TandokuTools }

function editenv { gvim (Convert-Path $TandokuModule) }
function reloadenv { Get-ChildItem $TandokuModule | Import-Module -Force }

function ConvertTo-KindleBook($source) {
    $title = RemoveAllExtensions($source)
    $epub = [IO.Path]::ChangeExtension($source, '.epub')

    ebook-convert $source $epub --epub-version=3 --language=ja --authors=Tandoku --title="$title"
    #pandoc $source -f commonmark+footnotes -o $epub -t epub3 --metadata title="$title" --metadata author=Tandoku --metadata lang=ja

	kindlegen $epub
}
Set-Alias tokindle ConvertTo-KindleBook

function RemoveAllExtensions($path) {
    $result = [IO.Path]::GetFilenameWithoutExtension($path)
    while ([IO.Path]::HasExtension($result)) {
        $result = [IO.Path]::GetFilenameWithoutExtension($result)
    }
    return $result
}

Set-Alias tandoku W:\tandoku\src\cli\bin\Debug\net5.0\tandoku.exe

Set-Alias subs2srs $TandokuTools\subs2srs\subs2srs.exe
Set-Alias SubtitleEdit $TandokuTools\SubtitleEdit\SubtitleEdit.exe

Export-ModuleMember -Function * -Alias *
