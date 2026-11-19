using System.Collections.Concurrent;
using EducationKeeptrack.Learning.Infrastructure;

namespace ApiTests;

// Submission photos live in memory during tests, so no test depends on (or writes to) a real R2 bucket.
public sealed class TestPhotoStore : ILearningPhotoStore
{
    private readonly ConcurrentDictionary<string, (byte[] Bytes, string ContentType)> objects = new();

    public int Count => objects.Count;
    public bool Contains(string key) => objects.ContainsKey(key);

    public Task PutPhotoAsync(string key, byte[] bytes, string contentType, CancellationToken ct)
    {
        objects[key] = (bytes, contentType);
        return Task.CompletedTask;
    }

    public Task<Stream> OpenPhotoAsync(string key, CancellationToken ct) =>
        objects.TryGetValue(key, out var item)
            ? Task.FromResult<Stream>(new MemoryStream(item.Bytes, writable: false))
            : throw new PhotoStorageException("Không tìm thấy ảnh.");

    public Task DeletePhotoAsync(string key, CancellationToken ct)
    {
        objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
