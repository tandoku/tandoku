param(
    [Parameter()]
    [String]
    $VolumePath
)

# TODO: infer $VolumePath if not specified

# TODO: set additional metadata in volume.yaml from Calibre, Kindle metadata
# (ISBN, ASIN, author, publisher, ...?)
# $opfMeta = TandokuCalibreExtractMeta -Path "$VolumePath/source/metadata.opf"
# $kindleMeta = TandokuKindleStoreExtractMeta -Path "$VolumePath/source/kindle-metadata.xml"

# TODO: stop using Kindle Store metadata, read additional metadata from temp/ebook/mobi8/OEBPS/content.opf instead.
# (unpack upfront before creating volume and use the simple title as volume title - can keep full title in other metadata if desired)
# Rationale - the only unique metadata in kindle-metadata.xml is the title pronunciation
# (original full title, e.g. with publishing magazine) and the purchase date.
# The unpacked mobi8/OEBPS/content.opf contains title/author/publisher sort for the simple title as well
# as other metadata like the ASIN, although some of it is under a comment block (would need to extract)
# (note that the ASIN is already in the metadata.opf though)
