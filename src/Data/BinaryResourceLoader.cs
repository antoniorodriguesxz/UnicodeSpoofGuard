using System.Collections.Concurrent;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;

namespace UnicodeSpoofGuard.Data;

internal static class BinaryResourceLoader
{
    private static readonly ConcurrentDictionary<string, BinaryResourceHandle> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string CacheRoot = Path.Combine(Path.GetTempPath(), "UnicodeSpoofGuard", "cache");

    public static BinaryResourceHandle MapEmbeddedResource(string resourceName, string fileName)
    {
        return Cache.GetOrAdd(resourceName, key => CreateHandle(key, fileName));
    }

    private static BinaryResourceHandle CreateHandle(string resourceName, string fileName)
    {
        Directory.CreateDirectory(CacheRoot);
        var cachePath = Path.Combine(CacheRoot, fileName);

        var assembly = Assembly.GetExecutingAssembly();
        using (var resourceStream = assembly.GetManifestResourceStream(resourceName)
               ?? throw new FileNotFoundException($"Embedded resource '{resourceName}' was not found."))
        {
            using var destination = File.Open(cachePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            resourceStream.CopyTo(destination);
        }

        var mmf = MemoryMappedFile.CreateFromFile(cachePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        return new BinaryResourceHandle(cachePath, mmf);
    }
}

internal sealed class BinaryResourceHandle : IDisposable
{
    private readonly MemoryMappedFile _memoryMappedFile;

    public BinaryResourceHandle(string path, MemoryMappedFile memoryMappedFile)
    {
        Path = path;
        _memoryMappedFile = memoryMappedFile;
    }

    public string Path { get; }

    public MemoryMappedViewStream CreateViewStream() =>
        _memoryMappedFile.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);

    public void Dispose()
    {
        _memoryMappedFile.Dispose();
    }
}

