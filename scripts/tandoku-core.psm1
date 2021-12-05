function New-TandokuVolume {
    param(
        [Parameter(Mandatory=$true)]
        [String]
        $Title,

        [Parameter()]
        [String]
        $ContainerPath,

        [Parameter()]
        [String]
        $Moniker
    )

    $libRoot = Get-TandokuLibraryRoot
    $libBlobRoot = Get-TandokuLibraryRoot -Blob
    $volumeFileName = (CleanInvalidPathChars $Title)
    if ($Moniker) {
        $volumeFileName = "$Moniker $volumeFileName"
    }
    $volumePath = (Join-Path $libRoot $ContainerPath $volumeFileName)
    $volumeBlobPath = (Join-Path $libBlobRoot $ContainerPath $volumeFileName)

    if (Test-Path $volumePath) {
        Write-Error "$volumePath already exists"
    } elseif (Test-Path $volumeBlobPath) {
        Write-Error "$volumeBlobPath already exists"
    } else {
        New-Item $volumePath -Type Directory
        [void](New-Item $volumeBlobPath -Type Directory)

        # TODO: create $volumeFileName.tdkv.yaml in $volumePath
        # with title, moniker
    }
}

function Import-TandokuContent {
    param(
        [Parameter(Mandatory=$true)]
        [String]
        $Path,

        [Parameter()]
        [String]
        $Volume,

        [Parameter()]
        [String]
        $TextLanguage = 'ja'
    )

    $resources = (Add-TandokuResource -Path $Path -Volume $Volume -RecognizeText -TextLanguage $TextLanguage)
    $volumeFileName = (Split-Path $Volume -Leaf)
    $contentPath = (Join-Path (Convert-Path $Volume) "$volumeFileName.tdkc.yaml")
    tandoku generate ($resources|Convert-Path) --out $contentPath
}

function Add-TandokuResource {
    param(
        [Parameter(Mandatory=$true)]
        [String]
        $Path,

        [Parameter()]
        [String]
        $Volume,

        [Parameter()]
        [Switch]
        $RecognizeText,

        [Parameter()]
        [String]
        $TextLanguage = 'ja'
    )

    $targetPath = (Get-TandokuPath $Volume -Blob)
    # TODO: only support image resources for now
    $targetPath = (Join-Path $targetPath 'images')
    if (-not (Test-Path $targetPath)) {
        [void](mkdir $targetPath)
    }

    $i = 0
    foreach ($item in (Get-ChildItem $Path|Sort-STNumerical)) {
        $i++
        $ext = (Split-Path $item -Extension)
        $targetItemPath = (Join-Path $targetPath "image$($i.ToString('d4'))$ext")
        Copy-Item $item $targetItemPath
        if ($RecognizeText) {
            [void](Add-AcvText $targetItemPath -Language $TextLanguage)
        }

        $targetItemPath
    }
}

function Get-TandokuLibraryRoot {
    param(
        [Parameter()]
        [Switch]
        $Blob
    )

    # TODO: get these from tandoku environment or based on current working directory
    if ($Blob) {
        return 'O:\tandoku\library'
    } else {
        return 'R:\tandoku-library'
    }
}

function Get-TandokuPath {
    param(
        [Parameter(Mandatory=$true)]
        [String]
        $Path,

        [Parameter()]
        [Switch]
        $Blob
    )

    if ($Blob) {
        if ($Path -match '\\tandoku-library\\(.+)$') {
            return (Join-Path 'O:\tandoku\library\' $matches[1])
        }
        throw "Path is not in tandoku library: $Path"
    } else {
        if ($Path -match '\\tandoku\\library\\(.+)$') {
            return (Join-Path 'R:\tandoku-library\' $matches[1])
        }
        throw "Path is not in tandoku library blob store: $Path"
    }
}
