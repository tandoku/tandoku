param(
    [Parameter()]
    [String]
    $ApiKey,

    [Parameter()]
    [Switch]
    $AudioOnly
)

if (-not $ApiKey) {
    $ApiKey = $env:RAPID_API_KEY
}
if (-not $ApiKey) {
    throw "Missing API key"
}

$headers=@{}
$headers.Add("X-RapidAPI-Key", $apiKey)
$headers.Add("X-RapidAPI-Host", "unogs-unogs-v1.p.rapidapi.com")

$lang = 'Japanese'
if ($AudioOnly) {
    $langArgs += "$lang-!-!"
} else {
    $langArgs += "$lang-!$lang-!"
}
$searchArgs = "q=-!1900%2C2999-!0%2C5-!0%2C10-!0-!Any-!$langArgs-!%7Bdownloadable%7D&t=ns&cl=78&st=adv&ob=FilmYear&sa=and"

$page = 0
$results = @()
do {
    $page++
    # V1 API - this seems to return more complete results than V2 API
    $response = Invoke-WebRequest -Uri "https://unogs-unogs-v1.p.rapidapi.com/aaapi.cgi?$searchArgs&p=$page" -Method GET -Headers $headers
    $json = $response.Content|ConvertFrom-Json
    $total = $json.count
    $results += $json.items
} while($page*100 -lt $total)

return $results
