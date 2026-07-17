using AsterERP.Api.Infrastructure.Abp.ObjectStorage;
using Volo.Abp.BlobStoring;

namespace AsterERP.Api.Infrastructure.Files;

public sealed class AbpBlobFileStorageService(
    IBlobContainer<AsterErpFileBlobContainer> blobContainer) : IFileStorageService
{
    public async Task<string> SaveAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        var key = $"{DateTime.UtcNow:yyyyMMdd}/{Guid.NewGuid():N}{extension}";
        await blobContainer.SaveAsync(key, stream, overrideExisting: false, cancellationToken);
        return key;
    }

    public async Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var blobName = NormalizeBlobName(relativePath);
        var stream = await blobContainer.GetOrNullAsync(blobName, cancellationToken);
        return stream ?? throw new FileNotFoundException("File not found.", blobName);
    }

    public async Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var blobName = NormalizeBlobName(relativePath);
        await blobContainer.DeleteAsync(blobName, cancellationToken);
    }

    private static string NormalizeBlobName(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException("Invalid file path.");
        }

        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        if (Path.IsPathRooted(relativePath) ||
            normalized.Contains("../", StringComparison.Ordinal) ||
            normalized.Contains("/..", StringComparison.Ordinal) ||
            normalized.Equals("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid file path.");
        }

        return normalized;
    }
}
