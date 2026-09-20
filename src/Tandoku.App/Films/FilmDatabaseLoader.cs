namespace Tandoku.App.Films;

using SharpYaml.Model;

internal static class FilmDatabaseLoader
{
    internal static async Task<IReadOnlyList<FilmRecord>> LoadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream);
        var yaml = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new InvalidDataException("The selected films.yaml file is empty.");
        }

        return await Task.Run(() => Parse(yaml), cancellationToken);
    }

    private static IReadOnlyList<FilmRecord> Parse(string yaml)
    {
        var yamlStream = YamlStream.Load(new StringReader(yaml));
        var films = new List<FilmRecord>(yamlStream.Count);
        for (var index = 0; index < yamlStream.Count; index++)
        {
            var film = yamlStream[index].Contents?.ToObject<FilmRecord>();
            if (film is null)
            {
                throw new InvalidDataException($"Film document #{index + 1} is empty.");
            }

            film.Prepare();
            films.Add(film);
        }

        if (films.Count == 0)
        {
            throw new InvalidDataException("The selected file does not contain any film documents.");
        }

        return films;
    }
}
