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

function Rename-SubtitlesToVideo {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        $SubtitleFilter,

        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        $VideoFilter,

        [Parameter()]
        [String]
        $Language,

        [Parameter()]
        [Switch] $Force
    )
    $subtitles = Get-ChildItem $SubtitleFilter
    $videos = Get-ChildItem $VideoFilter

    if ($subtitles.count -ne $videos.count) {
        throw 'Specified inputs for subtitles and videos have different lengths'
    }

    $subextlen = ($subtitles | Group-Object {[IO.Path]::GetExtension($_)}).Length
    if ($subextlen -ne 1 -and (-not $Force)) {
        throw 'Specified subtitles include multiple file extensions'
    }

    $vidextlen = ($videos | Group-Object {[IO.Path]::GetExtension($_)}).Length
    if ($vidextlen -ne 1 -and (-not $Force)) {
        throw 'Specified videos include multiple file extensions'
    }

    for ($i = 0; $i -lt $subtitles.Count; $i++) {
        $sub = $subtitles[$i]
        $vid = $videos[$i]
        $newext = [IO.Path]::GetExtension($sub)
        if ($Language) {
            $newext = ".$Language$newext"
        }
        $newname = [IO.Path]::ChangeExtension($vid, $newext)
        $sub | Rename-Item -NewName $newname
        $newname
    }
}

Export-ModuleMember -Function * -Alias *
