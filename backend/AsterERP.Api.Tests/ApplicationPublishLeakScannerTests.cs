using AsterERP.Api.Infrastructure.Publishing;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationPublishLeakScannerTests
{
    [Fact]
    public async Task ScanAsync_reports_marker_from_excluded_module_in_release_artifact()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteFile(
                root,
                "backend/AsterERP.Api/Controllers/SystemDictionaryController.cs",
                """
                namespace AsterERP.Api.Controllers;

                [Permission("system:dict:query")]
                public sealed class SystemDictionaryController
                {
                }
                """);
            WriteFile(root, "release/AsterERP.Api.dll", "compiled marker system:dict:query");
            var map = new ApplicationPublishModuleFileMap
            {
                Skeleton = ["backend/AsterERP.Api/Program.cs"],
                Modules =
                [
                    new ApplicationPublishModuleFileMapEntry
                    {
                        ModuleKey = "core.shell",
                        FileGlobs = ["backend/AsterERP.Api/Controllers/AuthController.cs"]
                    },
                    new ApplicationPublishModuleFileMapEntry
                    {
                        ModuleKey = "system.administration",
                        PermissionPrefixes = ["system:dict"],
                        FileGlobs = ["backend/AsterERP.Api/Controllers/SystemDictionaryController.cs"]
                    }
                ]
            };
            var snapshot = new ApplicationPublishDependencySnapshot(
                "WMS",
                null,
                "Trimmed",
                Array.Empty<object>(),
                Array.Empty<object>(),
                Array.Empty<object>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                ["core.shell"],
                Array.Empty<ApplicationPublishClosureEdge>(),
                Array.Empty<ApplicationPublishUnresolvedDependency>(),
                "hash");
            var scanner = new ApplicationPublishLeakScanner();

            var report = await scanner.ScanAsync(
                new ApplicationPublishLeakScanRequest(
                    root,
                    Path.Combine(root, "release"),
                    map,
                    snapshot),
                CancellationToken.None);

            Assert.Equal(1, report.ScannedFileCount);
            Assert.Contains(report.Findings, finding =>
                finding.ModuleKey == "system.administration" &&
                finding.Marker == "system:dict" &&
                finding.FilePath == "AsterERP.Api.dll");
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ScanAsync_skips_leak_detection_for_full_system_publish()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteFile(root, "release/AsterERP.Api.dll", "system:dict:query");
            var map = new ApplicationPublishModuleFileMap
            {
                Skeleton = ["backend/AsterERP.Api/Program.cs"],
                Modules =
                [
                    new ApplicationPublishModuleFileMapEntry
                    {
                        ModuleKey = "system.administration",
                        PermissionPrefixes = ["system:dict"]
                    }
                ]
            };
            var snapshot = new ApplicationPublishDependencySnapshot(
                "SYSTEM",
                null,
                "Full",
                Array.Empty<object>(),
                Array.Empty<object>(),
                Array.Empty<object>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                ["system.administration"],
                Array.Empty<ApplicationPublishClosureEdge>(),
                Array.Empty<ApplicationPublishUnresolvedDependency>(),
                "hash");
            var scanner = new ApplicationPublishLeakScanner();

            var report = await scanner.ScanAsync(
                new ApplicationPublishLeakScanRequest(
                    root,
                    Path.Combine(root, "release"),
                    map,
                    snapshot),
                CancellationToken.None);

            Assert.Empty(report.Findings);
            Assert.Equal(0, report.ForbiddenMarkerCount);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ScanAsync_does_not_attribute_resolved_permission_to_excluded_seed_source()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteFile(
                root,
                "backend/AsterERP.Api/Infrastructure/Abp/DevelopmentSeed/DevelopmentSeedDataService.cs",
                """
                public sealed class DevelopmentSeedDataService
                {
                    private const string PlatformPermission = "platform:application:publish";
                }
                """);
            WriteFile(
                root,
                "backend/AsterERP.Shared/Common/PermissionCodes.PlatformFoundation.cs",
                """
                public static partial class PermissionCodes
                {
                    public const string PlatformApplicationQuery = "platform:application:query";
                }
                """);
            WriteFile(root, "release/AsterERP.Api.dll", "compiled marker platform:application:publish");
            var map = new ApplicationPublishModuleFileMap
            {
                Skeleton = ["backend/AsterERP.Api/Program.cs"],
                Modules =
                [
                    new ApplicationPublishModuleFileMapEntry
                    {
                        ModuleKey = "development.seed",
                        FileGlobs = ["backend/AsterERP.Api/Infrastructure/Abp/DevelopmentSeed/DevelopmentSeedDataService.cs"]
                    },
                    new ApplicationPublishModuleFileMapEntry
                    {
                        ModuleKey = "platform.foundation",
                        PermissionPrefixes = ["platform:application"],
                        FileGlobs = ["backend/AsterERP.Shared/Common/PermissionCodes.PlatformFoundation.cs"]
                    }
                ]
            };
            var snapshot = new ApplicationPublishDependencySnapshot(
                "WMS",
                null,
                "Trimmed",
                Array.Empty<object>(),
                Array.Empty<object>(),
                Array.Empty<object>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                ["platform.foundation"],
                Array.Empty<ApplicationPublishClosureEdge>(),
                Array.Empty<ApplicationPublishUnresolvedDependency>(),
                "hash");
            var scanner = new ApplicationPublishLeakScanner();

            var report = await scanner.ScanAsync(
                new ApplicationPublishLeakScanRequest(
                    root,
                    Path.Combine(root, "release"),
                    map,
                    snapshot),
                CancellationToken.None);

            Assert.DoesNotContain(report.Findings, finding =>
                finding.ModuleKey == "development.seed" &&
                finding.Marker == "platform:application:publish");
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ScanAsync_ignores_third_party_dll_text_markers()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteFile(root, "release/ClosedXML.dll", "third party marker system:dict:query");
            var map = new ApplicationPublishModuleFileMap
            {
                Skeleton = ["backend/AsterERP.Api/Program.cs"],
                Modules =
                [
                    new ApplicationPublishModuleFileMapEntry
                    {
                        ModuleKey = "system.administration",
                        PermissionPrefixes = ["system:dict"],
                        FileGlobs = ["backend/AsterERP.Api/Controllers/SystemDictionaryController.cs"]
                    }
                ]
            };
            var snapshot = new ApplicationPublishDependencySnapshot(
                "WMS",
                null,
                "Trimmed",
                Array.Empty<object>(),
                Array.Empty<object>(),
                Array.Empty<object>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                ["core.shell"],
                Array.Empty<ApplicationPublishClosureEdge>(),
                Array.Empty<ApplicationPublishUnresolvedDependency>(),
                "hash");
            var scanner = new ApplicationPublishLeakScanner();

            var report = await scanner.ScanAsync(
                new ApplicationPublishLeakScanRequest(
                    root,
                    Path.Combine(root, "release"),
                    map,
                    snapshot),
                CancellationToken.None);

            Assert.Empty(report.Findings);
            Assert.Equal(0, report.ScannedFileCount);
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "AsterERP.LeakScannerTests", Guid.NewGuid().ToString("N"));
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
