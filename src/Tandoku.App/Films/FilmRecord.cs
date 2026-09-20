namespace Tandoku.App.Films;

using System.Globalization;
using System.Text.Json.Serialization;

public sealed class FilmRecord
{
    private static readonly string[] TvImdbTypes = ["tvSeries", "tvMiniSeries", "tvEpisode", "tvShort", "tvSpecial"];

    [JsonPropertyName("wikidata")]
    public string? Wikidata { get; set; }

    [JsonPropertyName("title")]
    public Dictionary<string, string> Titles { get; set; } = [];

    [JsonPropertyName("type")]
    public List<string> Types { get; set; } = [];

    [JsonPropertyName("country")]
    public List<string> Countries { get; set; } = [];

    [JsonPropertyName("language")]
    public List<string> Languages { get; set; } = [];

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("imdb")]
    public ImdbFilmInfo? Imdb { get; set; }

    [JsonPropertyName("myAnimeList")]
    public MyAnimeListInfo? MyAnimeList { get; set; }

    [JsonPropertyName("tmdb")]
    public TmdbFilmInfo? Tmdb { get; set; }

    [JsonPropertyName("natively")]
    public NativelyFilmInfo? Natively { get; set; }

    [JsonPropertyName("availability")]
    public FilmAvailability? Availability { get; set; }

    [JsonPropertyName("origin")]
    public List<string> Origins { get; set; } = [];

    [JsonIgnore]
    public string Title => GetValue(this.Titles, "en")
        ?? this.Titles.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
        ?? this.Imdb?.Title
        ?? this.Availability?.Netflix?.Title
        ?? "Untitled";

    [JsonIgnore]
    public string OriginalTitleDisplay => GetValue(this.Titles, "ja")
        ?? this.Imdb?.OriginalTitle
        ?? string.Empty;

    [JsonIgnore]
    public bool IsTv
    {
        get
        {
            if (this.Imdb?.Type is string imdbType)
            {
                return TvImdbTypes.Contains(imdbType, StringComparer.OrdinalIgnoreCase);
            }

            if (this.Tmdb?.Kind is string tmdbKind)
            {
                return string.Equals(tmdbKind, "tv-series", StringComparison.OrdinalIgnoreCase);
            }

            return this.Types.Any(type =>
                type.Contains("television", StringComparison.OrdinalIgnoreCase)
                || type.Contains("series", StringComparison.OrdinalIgnoreCase)
                || type.Contains("program", StringComparison.OrdinalIgnoreCase));
        }
    }

    [JsonIgnore]
    public bool Animated =>
        this.Types.Any(type =>
            type.Contains("anime", StringComparison.OrdinalIgnoreCase)
            || type.Contains("animated", StringComparison.OrdinalIgnoreCase))
        || (this.Imdb?.Genres.Contains("Animation", StringComparer.OrdinalIgnoreCase) ?? false);

    [JsonIgnore]
    public int? EffectiveNativelyLevel => this.Natively?.Level;

    [JsonIgnore]
    public double? EffectiveImdbRating => this.Imdb?.Rating;

    [JsonIgnore]
    public IReadOnlyList<string> EffectiveImdbLists => this.Imdb?.Lists.Keys.ToList() ?? [];

    [JsonIgnore]
    public string MediaTypeDisplay => this.IsTv ? "TV" : "MOVIE";

    [JsonIgnore]
    public string YearDisplay => (this.Year ?? this.Imdb?.Year ?? this.Availability?.Netflix?.Year)
        ?.ToString(CultureInfo.InvariantCulture)
        ?? "Year unknown";

    [JsonIgnore]
    public string RatingBadgeText => this.EffectiveImdbRating is double rating
        ? $"★ {rating:0.0}"
        : "★ —";

    [JsonIgnore]
    public string NativelyLevelDisplay => this.EffectiveNativelyLevel is int level
        ? $"Natively Lv {level}"
        : "Natively level unavailable";

    [JsonIgnore]
    public string FormatDisplay => this.Animated ? "Animated" : "Live action";

    [JsonIgnore]
    public string GenresDisplay => this.Imdb?.Genres.Count > 0
        ? string.Join(" · ", this.Imdb.Genres)
        : "Not specified";

    [JsonIgnore]
    public string RuntimeDisplay => this.Imdb?.Runtime is int runtime ? $"{runtime} min" : "Not specified";

    [JsonIgnore]
    public string SeriesDisplay => this.Imdb?.Episodes is int episodes
        ? $"{this.Imdb.Seasons?.ToString(CultureInfo.InvariantCulture) ?? "?"} seasons · {episodes} episodes"
        : string.Empty;

    [JsonIgnore]
    public string ImdbVotesDisplay => this.Imdb?.Votes is long votes
        ? votes.ToString("N0", CultureInfo.CurrentCulture)
        : "Not available";

    [JsonIgnore]
    public string ImdbListsDisplay => this.Imdb?.Lists.Count > 0
        ? string.Join(
            " · ",
            this.Imdb.Lists
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key} (#{pair.Value.ToString(CultureInfo.InvariantCulture)})"))
        : "Not on an imported IMDb list";

    [JsonIgnore]
    public string CountriesDisplay => this.Countries.Count > 0 ? string.Join(", ", this.Countries) : "Not specified";

    [JsonIgnore]
    public string LanguagesDisplay => this.Languages.Count > 0 ? string.Join(", ", this.Languages) : "Not specified";

    [JsonIgnore]
    public string AvailabilityDisplay => this.Availability?.Netflix is not null
        ? this.Availability.Netflix.Watchlist
            ? "Netflix · Watchlist"
            : "Netflix"
        : "Not specified";

    [JsonIgnore]
    public IReadOnlyList<FilmSourceLink> SourceLinks
    {
        get
        {
            var links = new List<FilmSourceLink>();
            AddLink(links, "Natively", this.Natively?.Url);

            if (!string.IsNullOrWhiteSpace(this.Imdb?.Id))
            {
                AddLink(links, "IMDb", $"https://www.imdb.com/title/{Uri.EscapeDataString(this.Imdb.Id)}/");
            }

            if (this.Tmdb?.Id is int tmdbId)
            {
                var tmdbMediaType = string.Equals(this.Tmdb.Kind, "movie", StringComparison.OrdinalIgnoreCase)
                    ? "movie"
                    : "tv";
                AddLink(
                    links,
                    "TMDB",
                    $"https://www.themoviedb.org/{tmdbMediaType}/{tmdbId.ToString(CultureInfo.InvariantCulture)}");
            }

            if (this.MyAnimeList?.Id is int myAnimeListId)
            {
                AddLink(
                    links,
                    "MyAnimeList",
                    $"https://myanimelist.net/anime/{myAnimeListId.ToString(CultureInfo.InvariantCulture)}");
            }

            if (this.Availability?.Netflix?.Id is int netflixId)
            {
                AddLink(
                    links,
                    "Netflix",
                    $"https://www.netflix.com/title/{netflixId.ToString(CultureInfo.InvariantCulture)}");
            }

            if (!string.IsNullOrWhiteSpace(this.Wikidata))
            {
                AddLink(
                    links,
                    "Wikidata",
                    $"https://www.wikidata.org/wiki/{Uri.EscapeDataString(this.Wikidata)}");
            }

            return links;
        }
    }

    [JsonIgnore]
    public string? PosterImageSource => this.Tmdb?.Images?.Small ?? this.Availability?.Netflix?.Images?.Small;

    [JsonIgnore]
    public string? DetailPosterImageSource => this.Tmdb?.Images?.Large
        ?? this.Availability?.Netflix?.Images?.Large
        ?? this.PosterImageSource;

    [JsonIgnore]
    public bool HasPoster => !string.IsNullOrWhiteSpace(this.PosterImageSource);

    internal void Prepare()
    {
        this.Titles = NormalizeDictionary(this.Titles);
        this.Types = NormalizeValues(this.Types);
        this.Countries = NormalizeValues(this.Countries);
        this.Languages = NormalizeValues(this.Languages);
        this.Origins = NormalizeValues(this.Origins);

        if (this.Imdb is not null)
        {
            this.Imdb.Genres = NormalizeValues(this.Imdb.Genres);
            this.Imdb.Lists = this.Imdb.Lists
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? GetValue(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static void AddLink(ICollection<FilmSourceLink> links, string label, string? url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && ((uri.Scheme == Uri.UriSchemeHttp) || (uri.Scheme == Uri.UriSchemeHttps)))
        {
            links.Add(new FilmSourceLink(label, uri.AbsoluteUri));
        }
    }

    private static Dictionary<string, string> NormalizeDictionary(IDictionary<string, string>? values) =>
        values?
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase)
        ?? [];

    private static List<string> NormalizeValues(IEnumerable<string>? values) =>
        values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
        ?? [];
}

public sealed record FilmSourceLink(string Label, string Url);

public sealed class ImdbFilmInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("originalTitle")]
    public string? OriginalTitle { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("endYear")]
    public int? EndYear { get; set; }

    [JsonPropertyName("runtime")]
    public int? Runtime { get; set; }

    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; } = [];

    [JsonPropertyName("adult")]
    public bool Adult { get; set; }

    [JsonPropertyName("seasons")]
    public int? Seasons { get; set; }

    [JsonPropertyName("episodes")]
    public int? Episodes { get; set; }

    [JsonPropertyName("rating")]
    public double? Rating { get; set; }

    [JsonPropertyName("votes")]
    public long? Votes { get; set; }

    [JsonPropertyName("lists")]
    public Dictionary<string, int> Lists { get; set; } = [];
}

public sealed class MyAnimeListInfo
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }
}

public sealed class TmdbFilmInfo
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("images")]
    public FilmImages? Images { get; set; }
}

public sealed class NativelyFilmInfo
{
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("level")]
    public int? Level { get; set; }

    [JsonPropertyName("temporaryLevel")]
    public bool TemporaryLevel { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public sealed class FilmAvailability
{
    [JsonPropertyName("netflix")]
    public NetflixAvailability? Netflix { get; set; }
}

public sealed class NetflixAvailability
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("matlabel")]
    public string? MaturityLabel { get; set; }

    [JsonPropertyName("images")]
    public FilmImages? Images { get; set; }

    [JsonPropertyName("watchlist")]
    public bool Watchlist { get; set; }
}

public sealed class FilmImages
{
    [JsonPropertyName("small")]
    public string? Small { get; set; }

    [JsonPropertyName("large")]
    public string? Large { get; set; }
}
