#:package Microsoft.Extensions.AI@*
#:package Microsoft.Extensions.AI.OpenAI@9.7.1-preview.1.25365.4
#:package Microsoft.Extensions.Caching.Abstractions@*

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;

var cache = new FileCache();

var openaiClient =
    new OpenAI.Chat.ChatClient("gpt-4o-mini", Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
    .AsIChatClient();

var client = openaiClient
    .AsBuilder()
    .UseDistributedCache(cache) // TODO - include model name in cache key
    .Build();

var response = await client.GetResponseAsync("Explain this text - たったん、たったん");
Console.WriteLine(response);

internal class FileCache : IDistributedCache
{
    public byte[]? Get(string key)
    {
        throw new NotImplementedException();
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        Console.WriteLine(key);
        Console.WriteLine(key.Length);
        throw new NotImplementedException();
    }

    public void Refresh(string key)
    {
        throw new NotImplementedException();
    }

    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public void Remove(string key)
    {
        throw new NotImplementedException();
    }

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        throw new NotImplementedException();
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }
}