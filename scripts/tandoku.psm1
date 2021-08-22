$TandokuRoot = 'O:\Tandoku'
$TandokuTools = "$TandokuRoot\Tools"

$TandokuScripts = (Split-Path $MyInvocation.MyCommand.Path -Parent)
$TandokuModules = "$TandokuScripts\tandoku*.psm1"
$TandokuRootModule = "$TandokuScripts\tandoku.psm1"
$TandokuSecondaryModules = "$TandokuScripts\tandoku-*.psm1"

#Consider adding the scripts directory to $env:PSModulePath rather than loading secondary modules upfront
Get-ChildItem $TandokuSecondaryModules | Import-Module 

function films { cd $TandokuRoot\Films }
function manga { cd $TandokuRoot\Manga }
function novels { cd $TandokuRoot\Novels }
function pbooks { cd "$TandokuRoot\Picture Books" }
function scripts { cd $TandokuScripts }
function tools { cd $TandokuTools }

function editenv { gvim (Convert-Path $TandokuRootModule) }
function reloadenv { Get-ChildItem $TandokuModules | Import-Module -Force }

# Use kindlegen from Kindle Previewer 3 installation since kindlegen download was discontinued
Set-Alias kindlegen "$env:LocalAppData\Amazon\Kindle Previewer 3\lib\fc\bin\kindlegen.exe"

function ConvertTo-KindleBook {
    param(
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [ValidateScript({
            if( -Not ($_ | Test-Path) ){
                throw "File or folder does not exist"
            }
            return $true
        })]
        [String] $Path,

        [ValidateSet('AZW3','MD')]
        [String] $TargetFormat
    )
    $title = RemoveAllExtensions($Path)

    $parentPath = (Split-Path $Path -Parent)
    $cover = (Join-Path $parentPath 'cover.jpg') 
    if (Test-Path $cover) {
        $otherParams = "--cover=`"$cover`""
    }

    Write-Verbose "Using additional parameters: $otherParams"

    if ($TargetFormat -eq 'AZW3') {
        $azw3 = [IO.Path]::ChangeExtension($Path, '.azw3')

        # NOTE: do not use --share-not-sync option as this breaks Vocabulary Builder
        ebook-convert $Path $azw3 --language=ja --authors=Tandoku --title="$title" $otherParams
    } else {
        $epub = [IO.Path]::ChangeExtension($Path, '.epub')

        ebook-convert $Path $epub --epub-version=3 --language=ja --authors=Tandoku --title="$title" $otherParams
        #pandoc $source -f commonmark+footnotes -o $epub -t epub3 --metadata title="$title" --metadata author=Tandoku --metadata lang=ja

        kindlegen $epub
    }
}
Set-Alias tokindle ConvertTo-KindleBook

function RemoveAllExtensions($path) {
    $result = [IO.Path]::GetFilenameWithoutExtension($path)
    while ([IO.Path]::HasExtension($result)) {
        $result = [IO.Path]::GetFilenameWithoutExtension($result)
    }
    return $result
}

Set-Alias tandoku W:\tandoku\src\cli\bin\Debug\net6.0\tandoku.exe

Set-Alias subs2srs $TandokuTools\subs2srs\subs2srs.exe
Set-Alias SubtitleEdit $TandokuTools\SubtitleEdit\SubtitleEdit.exe

Export-ModuleMember -Function * -Alias *
