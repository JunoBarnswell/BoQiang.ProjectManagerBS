namespace AsterERP.Api.Infrastructure.Files;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}
