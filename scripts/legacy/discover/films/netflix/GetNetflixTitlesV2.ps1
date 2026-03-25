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

$lang = 'japanese'
$searchArgs = "order_by=date_asc&country_list=78&audio=$lang"
if (-not $AudioOnly) {
    $searchArgs += "&subtitle=$lang&audio_sub_andor=and"
} else {
    $searchArgs += "&audio_sub_andor=or"
}

$offset = 0
$results = @()
do {
    # V2 API - doesn't seem to return all the results - https://forum.unogs.com/topic/145/api-on-rapidapi-return-fewer-results/4
    $response = Invoke-WebRequest -Uri "https://unogs-unogs-v1.p.rapidapi.com/search/titles?$searchArgs&offset=$offset" -Method GET -Headers $headers
    $json = $response.Content|ConvertFrom-Json
    $offset += $json.Object.limit
    $total = $json.Object.total
    $results += $json.results
} while($offset -lt $total)

return $results
