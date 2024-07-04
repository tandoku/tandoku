param(
    [Parameter()]
    [String]
    $VolumePath,

    [Parameter(Mandatory=$true)]
    [ValidateSet('acv4','easyocr')]
    [String]
    $Provider
)

switch ($Provider) {
    'acv4' {
        TandokuImagesAnalyze_acv4 -VolumePath $VolumePath
    }
    'easyocr' {
        TandokuImagesAnalyze_easyocr -VolumePath $VolumePath
    }
}
