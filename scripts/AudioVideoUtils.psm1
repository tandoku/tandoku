function Extract-Audio($source, $target) {
    if (Test-Path -Path $source -PathType Container) {
        #TODO: add argument for file extension, size
        Get-ChildItem *.vob|
            Where-Object Length -gt 50000000|
            Foreach-Object {
                [pscustomobject]@{
                    Source = $_
        #TODO: switch to sorting and using index instead of extracting from filename (could also pass in starting index as argument)
                    Target = [int]($_.Name -replace 'VTS_([0-9]+)_1.VOB','$1')-1
                }
            }|
            Foreach-Object {
                #Write-Output (Join-Path (Convert-Path $target) "ep$($_.Target).m4a")
                ffmpeg -i $_.Source -vn -acodec copy (Join-Path (Convert-Path $target) "ep$($_.Target).m4a")
            }
    } else {
        ffmpeg -i $source -vn -acodec copy $target
    }
}

Export-ModuleMember -Function * -Alias *
