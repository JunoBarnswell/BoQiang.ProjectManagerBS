using System.Security.Cryptography;

namespace AsterERP.Api.Infrastructure.Files;

public sealed class Sha256FileContentHashService : IFileContentHashService
{
    public async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken = default)
    {
        if (!stream.CanSeek)
        {
            throw new InvalidOperationException("File content stream must be seekable.");
        }

        stream.Position = 0;
        using var hash = SHA256.Create();
        var bytes = await hash.ComputeHashAsync(stream, cancellationToken);
        stream.Position = 0;
        return Convert.ToHexString(bytes);
    }
}
