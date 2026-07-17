using System.IO.Compression;
using AsterERP.Api.Infrastructure.Publishing;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationPublishPackageWriterTests
{
    [Fact]
    public async Task ZipPackage_includes_source_release_manifest_and_excludes_internal_logs()
    {
        var taskRoot = CreateTempDirectory();
        try
        {
            WriteFile(taskRoot, "source/backend/AsterERP.Api/Program.cs", "public sealed class ProgramMarker {}");
            WriteFile(taskRoot, "release/AsterERP.Api.dll", "runtime");
            WriteFile(taskRoot, "manifest/publish-manifest.json", "{}");
            WriteFile(taskRoot, "publish-logs/backend-publish.log", "log");
            WriteFile(taskRoot, "artifacts/old.zip", "old");

            var writer = new ApplicationPublishPackageWriter();
            await writer.WriteChecksumManifestAsync(
                taskRoot,
                Path.Combine(taskRoot, "manifest", "checksum-manifest.json"),
                CancellationToken.None);
            var result = await writer.ZipPackageAsync(
                taskRoot,
                Path.Combine(taskRoot, "artifacts", "WMS-task-1.zip"),
                CancellationToken.None);

            Assert.True(File.Exists(result.ArtifactPath));
            using var archive = ZipFile.OpenRead(result.ArtifactPath);
            var entries = archive.Entries
                .Select(entry => entry.FullName.Replace('\\', '/'))
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assert.Contains("source/backend/AsterERP.Api/Program.cs", entries);
            Assert.Contains("release/AsterERP.Api.dll", entries);
            Assert.Contains("manifest/publish-manifest.json", entries);
            Assert.Contains("manifest/checksum-manifest.json", entries);
            Assert.DoesNotContain(entries, entry => entry.StartsWith("publish-logs/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(entries, entry => entry.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDelete(taskRoot);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "AsterERP.PublishTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteFile(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void TryDelete(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Temp cleanup is best-effort; failed deletion should not mask test assertions.
        }
    }
}
