param(
    [Parameter(Mandatory=$true)]
    [String]
    $Path,

    [Parameter(Mandatory=$true)]
    [String]
    $Destination
)

# https://github.com/iscc/mobi, fork of https://github.com/kevinhendricks/KindleUnpack
# pip install mobi

[void] (mkdir $Destination)
mobiunpack -i --epub_version=3 $Path $Destination