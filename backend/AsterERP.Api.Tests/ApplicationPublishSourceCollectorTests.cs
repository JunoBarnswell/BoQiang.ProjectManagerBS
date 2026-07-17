using AsterERP.Api.Infrastructure.Publishing;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationPublishSourceCollectorTests
{
    [Fact]
    public async Task CreateAsync_for_trimmed_source_fails_when_declared_file_is_missing()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tempRoot = CreateTempDirectory();
        try
        {
            var collector = new ApplicationPublishSourceCollector();
            var map = new ApplicationPublishModuleFileMap
            {
                Skeleton = ["missing/declaration.ts"],
                Modules = []
            };

            await Assert.ThrowsAsync<FileNotFoundException>(() => collector.CreateAsync(
                new ApplicationPublishSourceRequest(
                    repositoryRoot,
                    Path.Combine(tempRoot, "source"),
                    Path.Combine(tempRoot, "manifest"),
                    "WMS",
                    "Trimmed",
                    [],
                    map),
                CancellationToken.None));
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [Fact]
    public async Task CreateAsync_rejects_legacy_runtime_registry_source_patterns()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tempRoot = CreateTempDirectory();
        try
        {
            var collector = new ApplicationPublishSourceCollector();
            var map = new ApplicationPublishModuleFileMap
            {
                Skeleton = [],
                Modules =
                [
                    new ApplicationPublishModuleFileMapEntry
                    {
                        ModuleKey = "runtime.core",
                        FileGlobs = ["frontend/AsterERP.Web/src/apps/runtimeRegistry.empty.ts"]
                    }
                ]
            };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => collector.CreateAsync(
                new ApplicationPublishSourceRequest(
                    repositoryRoot,
                    Path.Combine(tempRoot, "source"),
                    Path.Combine(tempRoot, "manifest"),
                    "WMS",
                    "Trimmed",
                    ["runtime.core"],
                    map),
                CancellationToken.None));

            Assert.Contains("Legacy runtime registry", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [Theory]
    [InlineData("../outside.ts")]
    [InlineData("frontend/../outside.ts")]
    [InlineData("C:/outside.ts")]
    public async Task CreateAsync_rejects_invalid_source_paths(string invalidPath)
    {
        var repositoryRoot = FindRepositoryRoot();
        var tempRoot = CreateTempDirectory();
        try
        {
            var collector = new ApplicationPublishSourceCollector();
            var map = new ApplicationPublishModuleFileMap
            {
                Skeleton = [invalidPath],
                Modules = []
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => collector.CreateAsync(
                new ApplicationPublishSourceRequest(
                    repositoryRoot,
                    Path.Combine(tempRoot, "source"),
                    Path.Combine(tempRoot, "manifest"),
                    "MES",
                    "Trimmed",
                    [],
                    map),
                CancellationToken.None));
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [Fact]
    public async Task CreateAsync_rejects_unknown_publish_mode()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tempRoot = CreateTempDirectory();
        try
        {
            var collector = new ApplicationPublishSourceCollector();
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => collector.CreateAsync(
                new ApplicationPublishSourceRequest(
                    repositoryRoot,
                    Path.Combine(tempRoot, "source"),
                    Path.Combine(tempRoot, "manifest"),
                    "MES",
                    "Legacy",
                    [],
                    new ApplicationPublishModuleFileMap()),
                CancellationToken.None));

            Assert.Contains("Unsupported publish mode", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [Fact]
    public async Task CreateAsync_for_wms_trimmed_source_excludes_non_closure_files()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tempRoot = CreateTempDirectory();
        try
        {
            var map = (await new ApplicationPublishModuleFileMapLoader().LoadAsync(repositoryRoot, CancellationToken.None)).Map;
            var collector = new ApplicationPublishSourceCollector();

            var result = await collector.CreateAsync(
                new ApplicationPublishSourceRequest(
                    repositoryRoot,
                    Path.Combine(tempRoot, "source"),
                    Path.Combine(tempRoot, "manifest"),
                    "WMS",
                    "Trimmed",
                    ["core.shell", "runtime.core"],
                    map),
                CancellationToken.None);

            var included = result.IncludedFiles
                .Select(file => file.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.Contains("backend/AsterERP.Shared/Common/PermissionCodes.cs", included);
            Assert.DoesNotContain(included, path => path.Contains("src/apps/runtimeRegistry.", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("frontend/AsterERP.Web/src/pages/dashboard/DashboardPage.target.tsx", included);
            Assert.DoesNotContain("backend/module-file-map.json", included);
            Assert.DoesNotContain("backend/AsterERP.Shared/Common/PermissionCodes.SystemAdministration.cs", included);
            Assert.DoesNotContain("backend/AsterERP.Shared/Common/PermissionCodes.PlatformFoundation.cs", included);
            Assert.DoesNotContain("backend/AsterERP.Shared/Common/PermissionCodes.PlatformPublish.cs", included);
            Assert.DoesNotContain("backend/AsterERP.Shared/Common/PermissionCodes.TenantApps.cs", included);
            Assert.DoesNotContain("frontend/AsterERP.Web/src/pages/dashboard/DashboardPage.tsx", included);
            Assert.DoesNotContain(included, path => path.Contains("src/pages/system/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(included, path => path.Contains("src/pages/platform/", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "AsterERP.SourceCollectorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AsterERP.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("无法定位仓库根目录。");
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
