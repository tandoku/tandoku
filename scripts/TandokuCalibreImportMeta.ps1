param(
    [Parameter()]
    [String]
    $VolumePath
)

# TODO: infer $VolumePath if not specified

# TODO: set additional metadata in volume.yaml from Calibre, Kindle metadata
# (ISBN, ASIN, author, publisher, ...?)
# $opfMeta = TandokuCalibreExtractMeta.ps1 -Path "$VolumePath/source/metadata.opf"
# $kindleMeta = TandokuKindleStoreExtractMeta.ps1 -Path "$VolumePath/source/kindle-metadata.xml"

# NOTE - the only unique metadata in kindle-metadata.xml is the title pronunciation
# (original full title, e.g. with publishing magazine) and the purchase date.
# The unpacked mobi8/OEBPS/content.opf contains title/author/publisher sort for the simple title as well
# as other metadata like the ASIN, although some of it is under a comment block (would need to extract)
