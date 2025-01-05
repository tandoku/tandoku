param(
    [Parameter()]
    [ValidateSet('epub','markdown')]
    [String]
    $Target = 'epub',

    [Parameter(Mandatory)]
    $Volume
)

# common variables
$volumePath = $volume.path
$volumeSlug = $volume.slug
$volumeLanguage = $volume.definition.language
$refLanguage = 'en'
$stagingEpub = TandokuConfig epub.staging
if (-not $stagingEpub) {
    # TODO - implement GetStagingPath <module> in tandoku-workflow-utils.psm1
    # which encodes fallback to <core.staging>/<module> and default value for
    # core.staging (~/.tandoku/staging)
    Write-Error 'Missing configuration for epub.staging'
    return
}

# configuration variables
# TODO - get any overrides from volume config
$config = @{
    alignSubtitlesNoFpsGuessing = $true
    alignSubtitlesNoSplit = $false
    extendAudio = 200
}

# initial_video artifact variables
$srcVideoPath = "$volumePath/source"
$initialVideoPath = "$volumePath/video"

# tandoku video init
TandokuVideoInit -Volume $volume

# timingref_subtitle artifact variables
$timingRefSubtitlePath = "$volumePath/subtitles/timing-ref"

# tandoku video extract-subtitles
TandokuVideoExtractSubtitles $initialVideoPath $timingRefSubtitlePath -Language en

# src_subtitle artifact variables
$srcSubtitlePath = "$volumePath/source"
$srcSubtitleRefPath = "$volumePath/source" #"$volumePath/source/ref-$refLanguage"
$initialSubtitlePath = "$volumePath/subtitles/00-initial"
$initialSubtitleRefPath = "$volumePath/subtitles/ref-$refLanguage/00-initial"

# tandoku subtitles init
TandokuSubtitlesInit $srcSubtitlePath $initialSubtitlePath -Language $volumeLanguage -Volume $volume
TandokuSubtitlesInit $srcSubtitleRefPath $initialSubtitleRefPath -Language $refLanguage -Volume $volume

# clean_subtitle artifact variables
$cleanSubtitlePath = "$volumePath/subtitles/10-clean"
$cleanSubtitleRefPath = "$volumePath/subtitles/ref-$refLanguage/10-clean"

# tandoku subtitles clean
TandokuSubtitlesClean $initialSubtitlePath $cleanSubtitlePath -Volume $volume
TandokuSubtitlesClean $initialSubtitleRefPath $cleanSubtitleRefPath -Volume $volume

# aligned_subtitle artifact variables
$alignedSubtitlePath = "$volumePath/subtitles/20-aligned"
$alignedSubtitleRefPath = "$volumePath/subtitles/ref-$refLanguage/20-aligned"
$subtitlesAlignReferencePath = $timingRefSubtitlePath # or $initialVideoPath if no reference subtitles available

# tandoku subtitles align
TandokuSubtitlesAlign $cleanSubtitlePath $alignedSubtitlePath -ReferencePath $subtitlesAlignReferencePath -Volume $volume -NoFpsGuessing:$config.alignSubtitlesNoFpsGuessing -NoSplit:$config.alignSubtitlesNoSplit
TandokuSubtitlesAlign $cleanSubtitleRefPath $alignedSubtitleRefPath -ReferencePath $subtitlesAlignReferencePath -Volume $volume -NoFpsGuessing:$config.alignSubtitlesNoFpsGuessing -NoSplit:$config.alignSubtitlesNoSplit

# initial_content artifact variables
$initialContentPath = "$volumePath/content/00-initial"
$initialContentRefPath = "$volumePath/content/ref-$refLanguage/00-initial"

# tandoku subtitles generate-content
tandoku subtitles generate-content $alignedSubtitlePath $initialContentPath
tandoku subtitles generate-content $alignedSubtitleRefPath $initialContentRefPath

# merged_content artifact variables
$mergedContentPath = "$volumePath/content/10-merged"

# tandoku content merge
tandoku content merge $initialContentPath $initialContentRefPath $mergedContentPath --align timecodes --ref $refLanguage

# media_extraction_subtitles artifact variables
$mediaExtractionSubtitlesPath = "$volumePath/media/10-subtitles"

tandoku subtitles generate $mergedContentPath $mediaExtractionSubtitlesPath --purpose mediaExtraction --include-ref $refLanguage --extend-audio $config.extendAudio

# extracted_media artifact variables
$extractedMediaPath = "$volumePath/media/20-media"

TandokuVideoExtractMedia $mediaExtractionSubtitlesPath $extractedMediaPath -Volume $volume

# media_content artifact variables
$mediaContentPath = "$volumePath/content/20-media"

# tandoku media import
TandokuMediaImport $mergedContentPath $mediaContentPath $extractedMediaPath -Volume $volume

# images_analyze workflow step
TandokuImagesAnalyze $mediaContentPath -Provider acv4 -Volume $volume

# analyzed_image_content artifact variables
$analyzedImageContentPath = "$volumePath/content/30-analyzed-images"

tandoku content transform import-image-text $mediaContentPath $analyzedImageContentPath --provider acv4 --role on-screen-text --volume $volumePath

<#
# content_remove-low-confidence-text artifact variables
$contentDirectory40 = "$volumePath/content/40-remove-low-confidence-text"

# content_transform_remove-low-confidence-text workflow step
tandoku content transform remove-low-confidence-text $analyzedImageContentPath $contentDirectory40
#>

# content_remove-non-japanese-text artifact variables
$contentDirectory50 = "$volumePath/content/50-remove-non-japanese-text"

tandoku content transform remove-non-japanese-text --role on-screen-text $analyzedImageContentPath $contentDirectory50

# content_merge-ref-chunks artifact variables
$mergeRefChunksContentPath = "$volumePath/content/60-merge-ref-chunks"

tandoku content transform merge-ref-chunks $contentDirectory50 $mergeRefChunksContentPath

# markdown artifact variables
$markdownPath = "$volumePath/markdown"

# tandoku markdown export
TandokuMarkdownExport $mergeRefChunksContentPath $markdownPath -NoHeadings -ReferenceBehavior Footnotes -ReferenceLabels None -Quirks KyBook3

if ($Target -eq 'epub') {
    # TODO - generic workflow mechanism for splitting inputs to multiple targets?
    # Or just implement this in TandokuEpubExport for now
    # Consider sharing some implementation with content generation from CSV
    <#
    $epubGroups = @(
        @{ inputMask = '*s01e0[1-4].*'; epubSuffix = '-1-4' },
        @{ inputMask = '*s01e0[5-8].*'; epubSuffix = '-5-8' },
        @{ inputMask = '*s01e09.*','*s01e1[0-1].*'; epubSuffix = '-9-11' }
    )

    foreach ($epubGroup in $epubGroups) {
        # epub artifact variables
        $epubPath = "$stagingEpub/$volumeSlug$($epubGroup.epubSuffix).epub"

        # markdown split variables
        $markdownFiles = Get-ChildItem "$markdownPath/*.md" -Include $epubGroup.inputMask

        if ($markdownFiles) {
            # tandoku epub export
            TandokuEpubExport $markdownFiles $epubPath -TitleSuffix $epubGroup.epubSuffix
        }
    }
    #>

    # epub artifact variables
    $epubPath = "$stagingEpub/$volumeSlug.epub"

    # tandoku epub export
    TandokuEpubExport $markdownPath $epubPath
}