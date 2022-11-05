$nextUrl = 'https://jpdb.io/anime-difficulty-list'

do {
    $response = Invoke-WebRequest $nextUrl
    $html = $response.Content | ConvertFrom-Html

    # title list
    $html.SelectNodes('//h5') |
        ForEach-Object {
            $details = $_.NextSibling
            [PSCustomObject] @{
                Title = $_.InnerText
                Details = $details.InnerText
            }
        }

    # next page
    if ($nextUrl -match 'offset') { break } #testing
    $nextUrl = $html.SelectNodes('//a') |
        Where-Object InnerText -eq 'Next page' |
        Select-Object -First 1 |
        ForEach-Object { $_.Attributes['href'].Value }
    if ($nextUrl) {
        $nextUrl = 'https://jpdb.io' + $nextUrl
    }
} while($nextUrl)