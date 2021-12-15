function Extract-ImagesFromPdf($path) {
    #alternate with xpdf-utils: pdfimages -j $path image
    #pdfimages can't handle non-ASCII characters in $path though (ok if filename only specified and parent path has non-ASCII though)

    mutool extract $path
}

Export-ModuleMember -Function *-* -Alias *
