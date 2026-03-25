param(
    [Parameter()]
    [ValidateSet('anime', 'live-action', 'novel', 'visual-novel', 'web-novel')]
    [String]
    $ContentType,

    [Parameter()]
    [Switch]
    $FirstPageOnly
)

# TODO: support auth token so known word stats can be extracted

$listUrlSegment = $ContentType.ToLowerInvariant()
$nextUrl = "https://jpdb.io/$listUrlSegment-difficulty-list"

do {
    $response = Invoke-WebRequest $nextUrl
    $html = $response.Content | ConvertFrom-Html

    # title list
    $html.SelectNodes('//h5') |
        ForEach-Object {
            $obj = @{
                Title = $_.InnerText
            }
            foreach ($detail in $_.NextSibling.SelectNodes('table/tr/th')) {
                $obj[$detail.InnerText] = $detail.NextSibling.InnerText
            }
            foreach ($link in $_.NextSibling.SelectNodes('div/a')) {
                $obj[$link.InnerText] = $link.Attributes['href'].Value
            }
            [PSCustomObject] $obj
        }

    if ($FirstPageOnly -and $nextUrl -match 'offset') {
        break
    }

    # next page
    $nextUrl = $html.SelectNodes('//a') |
        Where-Object InnerText -eq 'Next page' |
        Select-Object -First 1 |
        ForEach-Object { $_.Attributes['href'].Value }
    if ($nextUrl) {
        $nextUrl = 'https://jpdb.io' + $nextUrl
    }
} while($nextUrl)
