namespace Tandoku.Subtitles.Ttml;

using System.Xml.Serialization;

public static class TtmlSerializer
{
    /// <summary>
    /// Asynchronously deserializes a TTML document from a stream.
    /// </summary>
    public static async Task<TtmlDocument> DeserializeAsync(Stream stream)
    {
        if (stream is not MemoryStream memoryStream)
        {
            memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream).ConfigureAwait(false);
            memoryStream.Position = 0; // Reset position for reading
        }
        return Deserialize(memoryStream);
    }

    /// <summary>
    /// Synchronously deserializes a TTML document from a stream.
    /// </summary>
    public static TtmlDocument Deserialize(Stream stream)
    {
        var serializer = new XmlSerializer(typeof(TtmlDocument));
        return serializer.Deserialize(stream) as TtmlDocument ??
            throw new InvalidDataException("Failed to deserialize TTML document.");
    }
}