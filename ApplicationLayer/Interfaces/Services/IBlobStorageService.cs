namespace ApplicationLayer.Interfaces.Services;

public interface IBlobStorageService
{
    Task<string> UploadAsync(Stream fileStream, string contentType, string blobPath, CancellationToken ct = default);
    Task DeleteAsync(string blobPath, CancellationToken ct = default);
    Task<string> GenerateReadSasUrlAsync(string blobPath, TimeSpan expiry, CancellationToken ct = default);
    /// <summary>SCRUM-486: đọc bytes blob (md/txt viewer server-side).</summary>
    Task<byte[]> DownloadAsync(string blobPath, CancellationToken ct = default);
}
