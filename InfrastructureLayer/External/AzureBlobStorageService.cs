using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Settings;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.External;

public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobStorageSettings _settings;
    private BlobContainerClient? _container;

    public AzureBlobStorageService(IOptions<BlobStorageSettings> settings)
    {
        _settings = settings.Value;
    }

    private BlobContainerClient GetContainer()
    {
        if (_container is not null)
            return _container;

        if (string.IsNullOrWhiteSpace(_settings.ConnectionString))
            throw new InvalidOperationException("BlobStorage:ConnectionString chưa được cấu hình.");

        var serviceClient = new BlobServiceClient(_settings.ConnectionString);
        _container = serviceClient.GetBlobContainerClient(_settings.ContainerName);
        _container.CreateIfNotExists();
        return _container;
    }

    public async Task<string> UploadAsync(Stream fileStream, string contentType, string blobPath, CancellationToken ct = default)
    {
        var container = GetContainer();
        var blob = container.GetBlobClient(blobPath);
        await blob.UploadAsync(fileStream, new Azure.Storage.Blobs.Models.BlobUploadOptions
        {
            HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = contentType }
        }, ct);
        return blobPath;
    }

    public async Task DeleteAsync(string blobPath, CancellationToken ct = default)
    {
        var container = GetContainer();
        var blob = container.GetBlobClient(blobPath);
        await blob.DeleteIfExistsAsync(cancellationToken: ct);
    }

    public Task<string> GenerateReadSasUrlAsync(string blobPath, TimeSpan expiry, CancellationToken ct = default)
    {
        var container = GetContainer();
        var blob = container.GetBlobClient(blobPath);
        if (!blob.CanGenerateSasUri)
            throw new InvalidOperationException("Blob client không thể tạo SAS — cần connection string có account key.");

        var sas = new BlobSasBuilder
        {
            BlobContainerName = container.Name,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry)
        };
        sas.SetPermissions(BlobSasPermissions.Read);

        return Task.FromResult(blob.GenerateSasUri(sas).ToString());
    }
}
