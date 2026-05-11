namespace Tandoku.Tests.Content;

using System.Collections.Concurrent;
using Lucene.Net.Store;
using Tandoku.Content;

/// <summary>
/// In-memory <see cref="ILuceneDirectoryFactory"/> backed by <see cref="RAMDirectory"/>.
/// Open() on the same path returns the same underlying directory so writers
/// and readers can share state without touching disk.
/// </summary>
internal sealed class RamDirectoryFactory : ILuceneDirectoryFactory
{
    private readonly ConcurrentDictionary<string, RAMDirectory> directories = new();

    public Directory Open(string path) =>
        new NonDisposingDirectory(this.directories.GetOrAdd(path, _ => new RAMDirectory()));

    /// <summary>
    /// Wraps a RAMDirectory so disposal by `using` blocks in production code
    /// doesn't tear down our shared instance between Build/Search invocations.
    /// </summary>
    private sealed class NonDisposingDirectory(RAMDirectory inner) : Directory
    {
        public override string[] ListAll() => inner.ListAll();
        [Obsolete("Override of obsolete member; required by base class.")]
        public override bool FileExists(string name) => inner.FileExists(name);
        public override void DeleteFile(string name) => inner.DeleteFile(name);
        public override long FileLength(string name) => inner.FileLength(name);
        public override IndexOutput CreateOutput(string name, IOContext context) =>
            inner.CreateOutput(name, context);
        public override void Sync(System.Collections.Generic.ICollection<string> names) =>
            inner.Sync(names);
        public override IndexInput OpenInput(string name, IOContext context) =>
            inner.OpenInput(name, context);
        public override Lock MakeLock(string name) => inner.MakeLock(name);
        public override void ClearLock(string name) => inner.ClearLock(name);
        public override void SetLockFactory(LockFactory lockFactory) =>
            inner.SetLockFactory(lockFactory);
        public override LockFactory LockFactory => inner.LockFactory;

        protected override void Dispose(bool disposing)
        {
            // intentionally do not dispose the shared inner directory
        }
    }
}
