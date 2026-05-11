# audio/video/subtitle tools
scoop install ffmpeg
scoop install alass # https://github.com/kaegi/alass
scoop install subtitleedit # https://github.com/SubtitleEdit/subtitleedit

scoop install python
pip install wheel
pip install subs2cia # https://github.com/dxing97/subs2cia

# ebook/kindle tools
if (-not (Test-Command ebook-convert)) {
    # NOTE: may want to use calibre-normal to keep library outside of scoop
    # but this will work to install calibre CLI tools
    scoop install calibre
}
pip install mobi # https://github.com/iscc/mobi, fork of https://github.com/kevinhendricks/KindleUnpack

# pdf tools
scoop install mupdf
