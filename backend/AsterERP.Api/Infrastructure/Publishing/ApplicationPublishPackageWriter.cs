using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace AsterERP.Api.Infrastructure.Publishing;

public sealed record ApplicationPublishPackageResult(string ArtifactPath, string Sha256, long SizeBytes);

public sealed class ApplicationPublishPackageWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task WriteManifestAsync(
        string manifestPath,
        ApplicationPublishManifest manifest,
        CancellationToken cancellationToken)
    {
        await WriteJsonAsync(manifestPath, manifest, cancellationToken);
    }

    public async Task WriteJsonAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
    }

    public async Task WriteChecksumManifestAsync(
        string root,
        string manifestPath,
        CancellationToken cancellationToken)
    {
        var entries = new List<ApplicationPublishDependencyFile>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativeFile = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (ShouldSkipPackagedPath(relativeFile))
            {
                continue;
            }

            if (Path.GetFullPath(file).Equals(Path.GetFullPath(manifestPath), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            var info = new FileInfo(file);
            entries.Add(new ApplicationPublishDependencyFile(
                relativeFile,
                "checksum",
                "checksum",
                Convert.ToHexString(hash),
                info.Length));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await using var manifestStream = new FileStream(manifestPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await JsonSerializer.SerializeAsync(manifestStream, entries.OrderBy(item => item.Path).ToList(), JsonOptions, cancellationToken);
    }

    public async Task<ApplicationPublishPackageResult> ZipReleaseAsync(
        string releaseRoot,
        string artifactPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
        if (File.Exists(artifactPath))
        {
            File.Delete(artifactPath);
        }

        ZipFile.CreateFromDirectory(releaseRoot, artifactPath, CompressionLevel.Fastest, includeBaseDirectory: false);
        await using var stream = new FileStream(artifactPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        var info = new FileInfo(artifactPath);
        return new ApplicationPublishPackageResult(artifactPath, Convert.ToHexString(hash), info.Length);
    }

    public async Task<ApplicationPublishPackageResult> ZipPackageAsync(
        string taskRoot,
        string artifactPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
        if (File.Exists(artifactPath))
        {
            File.Delete(artifactPath);
        }

        using (var archive = ZipFile.Open(artifactPath, ZipArchiveMode.Create))
        {
            foreach (var file in Directory.EnumerateFiles(taskRoot, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativeFile = Path.GetRelativePath(taskRoot, file).Replace('\\', '/');
                if (ShouldSkipPackagedPath(relativeFile))
                {
                    continue;
                }

                archive.CreateEntryFromFile(file, relativeFile, CompressionLevel.Fastest);
            }
        }

        await using var stream = new FileStream(artifactPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        var info = new FileInfo(artifactPath);
        return new ApplicationPublishPackageResult(artifactPath, Convert.ToHexString(hash), info.Length);
    }

    private static bool ShouldSkipPackagedPath(string relativePath)
    {
        return relativePath.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase) ||
               relativePath.StartsWith("publish-logs/", StringComparison.OrdinalIgnoreCase);
    }
}
