namespace ApplicationLayer.Interfaces.Services;

public interface IBlobStorageService
{
    Task<string> UploadAsync(Stream fileStream, string contentType, string blobPath, CancellationToken ct = default);
    Task DeleteAsync(string blobPath, CancellationToken ct = default);
    Task<string> GenerateReadSasUrlAsync(string blobPath, TimeSpan expiry, CancellationToken ct = default);
}
