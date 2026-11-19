namespace EducationKeeptrack.Learning.Infrastructure;

public interface ILearningPhotoStore
{
    Task PutPhotoAsync(string key, byte[] bytes, string contentType, CancellationToken ct);
    Task<Stream> OpenPhotoAsync(string key, CancellationToken ct);
    Task DeletePhotoAsync(string key, CancellationToken ct);
}

public sealed class PhotoStorageException : Exception
{
    public PhotoStorageException(string message) : base(message) { }
    public PhotoStorageException(string message, Exception inner) : base(message, inner) { }
}
