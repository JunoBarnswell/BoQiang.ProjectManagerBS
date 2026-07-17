namespace AsterERP.Api.Infrastructure.Files;

public interface IFileContentHashService
{
    Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken = default);
}
