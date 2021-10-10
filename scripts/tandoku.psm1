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

        [ValidateSet('AZW3','MOBI')]
        [String] $TargetFormat,

        [String] $Title
    )
    if (-not $Title) {
        $title = RemoveAllExtensions($Path)
    }

    $parentPath = (Split-Path $Path -Parent)
    $cover = (Join-Path $parentPath 'cover.jpg') 
    if (Test-Path $cover) {
        $otherParams = "--cover=`"$cover`""
    }

    Write-Verbose "Using additional parameters: $otherParams"

    if ($TargetFormat -eq 'AZW3') {
        $azw3 = [IO.Path]::ChangeExtension($Path, '.azw3')

        # NOTE: do not use --share-not-sync option as this breaks Vocabulary Builder
        ebook-convert $Path $azw3 --language=ja --authors=tandoku --title="$title" $otherParams
    } else {
        $epub = [IO.Path]::ChangeExtension($Path, '.epub')

        ebook-convert $Path $epub --epub-version=3 --language=ja --authors=tandoku --title="$title" $otherParams
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

function Compress-TandokuVolume {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param(
        [Parameter(Mandatory=$false, ValueFromPipeline=$true)]
        [ValidateScript({
            if( -Not ($_ | Test-Path) ){
                throw "File or folder does not exist"
            }
            return $true
        })]
        [String] $Path = '.',

        [Switch] $ImagesAsCbz,

        [Switch] $ImageText
    )
    process {
        Get-ChildItem -Path $Path -Filter images -Recurse -Directory |
            Foreach-Object {
                $volumePath = (Split-Path $_ -Parent)
                Push-Location $volumePath

                # TODO: move this to a separate cmdlet?
                $coverPath = 'cover.jpg'
                if (-not (Test-Path $coverPath)) {
                  Get-ChildItem ./images/cover*.jp*g |
                    Foreach-Object {
                      Write-Verbose "Copying $_ to $coverPath"
                      Copy-Item $_ $coverPath
                    }
                } else {
                    Write-Verbose "$(Resolve-Path $coverPath) already exists"
                }

                $title = (Split-Path $volumePath -Leaf)

                if ($ImagesAsCbz) {
                    $cbzPath = "$title.cbz"
                    if ((Test-Path $coverPath) -and (-not (Test-Path $cbzPath))) {
                      Write-Verbose "Creating $cbzPath from images"
                      Compress-Archive ./images/*.* $cbzPath

                      # verify all items added
                      $cbzFileCount = (7z l $cbzPath|sls '(\d+) files$').Matches[0].Groups[1].Value
                      $imgFileCount = (Get-ChildItem ./images/*.*).Count
                      if ($cbzFileCount -eq $imgFileCount) {
                          Write-Verbose "Removing $imgFileCount files from images"
                          Remove-Item ./images/*.*
                      }
                    } elseif (-not (Test-Path $coverPath)) {
                        Write-Verbose "Missing $coverPath, skipping .cbz archive for $title"
                    } else {
                        Write-Verbose "$(Resolve-Path $cbzPath) already exists"
                    }
                }

                if ($ImageText) {
                    $tdzPath = "$title.tdz"
                    if (Test-Path ./images/text/*.*) {
                        Write-Verbose "Copying ./images/text/ to $tdzPath"
                        7z a -spf -tzip $tdzPath images/text/*.*

                        # verify all items added
                        $tdzFileCount = (7z l $tdzPath|sls '(\d+) files$').Matches[0].Groups[1].Value
                        $textFileCount = (Get-ChildItem ./images/text/*.*).Count
                        if ($tdzFileCount -eq $textFileCount) {
                            Write-Verbose "Removing $textFileCount files from images/text"
                            Remove-Item ./images/text/*.*
                        }
                    } else {
                        Write-Verbose "No images/text files found for $title"
                    }
                }

                Pop-Location
            }
    }
}

Set-Alias tandoku W:\tandoku\src\cli\bin\Debug\net6.0\tandoku.exe

Set-Alias subs2srs $TandokuTools\subs2srs\subs2srs.exe
Set-Alias SubtitleEdit $TandokuTools\SubtitleEdit\SubtitleEdit.exe

Export-ModuleMember -Function * -Alias *
