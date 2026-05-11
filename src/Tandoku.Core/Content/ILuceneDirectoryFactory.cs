namespace Tandoku.Content;

using Lucene.Net.Store;

internal interface ILuceneDirectoryFactory
{
    Directory Open(string path);
}

internal sealed class FSDirectoryFactory : ILuceneDirectoryFactory
{
    internal static readonly FSDirectoryFactory Instance = new();

    public Directory Open(string path) => FSDirectory.Open(path);
}
