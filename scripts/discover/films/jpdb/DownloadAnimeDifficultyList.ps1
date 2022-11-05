$nextUrl = 'https://jpdb.io/anime-difficulty-list'

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

    # Uncomment this to break after second page
    #if ($nextUrl -match 'offset') { break }

    # next page
    $nextUrl = $html.SelectNodes('//a') |
        Where-Object InnerText -eq 'Next page' |
        Select-Object -First 1 |
        ForEach-Object { $_.Attributes['href'].Value }
    if ($nextUrl) {
        $nextUrl = 'https://jpdb.io' + $nextUrl
    }
} while($nextUrl)