function Import-GcvOcrContent {
    Get-ChildItem images -Filter *.gcv.json -Recurse |
        Get-Content |
        ConvertFrom-Json
}

function Get-GcvOcrContentForImage($imagePath) {
    if (-not $imagePath) {
        $imagePath = [String] (Get-Clipboard)
        $imagePath = $imagePath.Trim('"')
    }

    $gcvOcrFilename = [IO.Path]::GetFileName([IO.Path]::ChangeExtension($imagePath, '.gcv.json'))
    $gcvOcrPath = [IO.Path]::Combine([IO.Path]::GetDirectoryName($imagePath), 'ocr')
    $gcvOcrPath = [IO.Path]::Combine($gcvOcrPath, $gcvOcrFilename)

    if (-not (Test-Path $gcvOcrPath)) {
        Write-Error "Path does not exist: $gcvOcrPath"
        return
    }

    $json = Get-Content -LiteralPath $gcvOcrPath | ConvertFrom-Json
    $json.responses[0].fullTextAnnotation.text
}

Export-ModuleMember -Function * -Alias *
