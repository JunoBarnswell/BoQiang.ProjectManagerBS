using System.Text.Json;
using Xunit;

namespace AsterERP.Api.Tests;

/// <summary>
/// Executable audit for the latest-only boundary. This deliberately does not
/// delete or ignore unresolved production dependencies: it requires every
/// remaining dependency to be named in the audit record and blocks any new
/// dependency from silently entering the product route.
/// </summary>
public sealed class LatestOnlyArchitectureAuditTests
{
    private static readonly string[] ForbiddenLatestOnlyTokens =
    [
        "DesignerRuntimeRenderer",
        "runtimeDocumentCodec",
        "PageRenderer",
        "LegacyRuntimeRenderer",
        "LegacyCompiler",
        "runtimeRegistry",
        "UseLegacyRuntime",
        "dualRead",
        "dualWrite",
        "shadowRender",
        "shadowRendering",
        "FeatureFlag",
        "featureFlag"
    ];

    [Fact]
    public void Production_full_designer_dependencies_are_explicitly_audited()
    {
        var root = FindRepositoryRoot();
        var actual = FindProductionFullDesignerDependencies(root);
        var audit = ReadAudit(root);
        var expected = audit.GetProperty("productionDependencies").EnumerateArray()
            .Select(item => item.GetProperty("path").GetString() + " => " + item.GetProperty("target").GetString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(expected.OrderBy(value => value), actual.OrderBy(value => value));

        foreach (var dependency in audit.GetProperty("productionDependencies").EnumerateArray())
        {
            Assert.Equal("Blocked", dependency.GetProperty("status").GetString());
            Assert.False(string.IsNullOrWhiteSpace(dependency.GetProperty("requiredOwner").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(dependency.GetProperty("removalAction").GetString()));
        }
    }

    [Fact]
    public void Latest_production_chain_has_no_deleted_runtime_or_dual_track_tokens()
    {
        var root = FindRepositoryRoot();
        var files =
        new[]
        {
            "frontend/AsterERP.Web/src/pages/runtime/RuntimePage.tsx",
            "frontend/AsterERP.Web/src/runtime-kernel/RuntimeKernel.ts",
            "frontend/AsterERP.Web/src/runtime-kernel/ComponentRuntimeHost.tsx",
            "frontend/AsterERP.Web/src/pages/application-console/development-center/DevelopmentCenterDesignerPage.tsx",
            "frontend/AsterERP.Web/src/pages/application-console/development-center/low-code-studio",
            "backend/AsterERP.Api/Application/ApplicationDevelopmentCenter",
            "backend/AsterERP.Contracts/ApplicationDevelopmentCenter"
        }
        .SelectMany(path => EnumerateSourceFiles(Path.Combine(root, path)))
        .Distinct(StringComparer.OrdinalIgnoreCase);

        var findings = files.SelectMany(file =>
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var lines = File.ReadAllLines(file);
            return lines.SelectMany((line, index) => ForbiddenLatestOnlyTokens
                .Where(token => line.Contains(token, StringComparison.OrdinalIgnoreCase))
                .Select(token => $"{relative}:{index + 1}:{token}"));
        }).ToArray();

        Assert.Empty(findings);
    }

    [Fact]
    public void Designer_mode_and_publish_artifact_entry_are_latest_only()
    {
        var root = FindRepositoryRoot();
        var designerPage = Read(root, "frontend/AsterERP.Web/src/pages/application-console/development-center/DevelopmentCenterDesignerPage.tsx");
        var compiler = Read(root, "backend/AsterERP.Api/Application/ApplicationDevelopmentCenter/ApplicationDevelopmentSchemaCompiler.cs");
        var runtimePage = Read(root, "frontend/AsterERP.Web/src/pages/runtime/RuntimePage.tsx");
        var contract = Read(root, "docs/contracts/designer-document.latest.schema.json");

        Assert.Contains("designerMode: 'structured'", designerPage, StringComparison.Ordinal);
        Assert.DoesNotContain("designerMode: 'v3'", designerPage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("designerMode: 'v4'", designerPage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[\"renderer\"] = \"designerDocument\"", compiler, StringComparison.Ordinal);
        Assert.Contains("[\"migrationRevision\"] = \"latest\"", compiler, StringComparison.Ordinal);
        Assert.Contains("[\"signature\"] = signature", compiler, StringComparison.Ordinal);
        Assert.Contains("parseRuntimePageArtifact", runtimePage, StringComparison.Ordinal);
        Assert.Contains("toRuntimeArtifact", runtimePage, StringComparison.Ordinal);
        Assert.Contains("ComponentRuntimeHost", runtimePage, StringComparison.Ordinal);
        Assert.DoesNotContain("schemaVersion", contract, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"v3\"", contract, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"v4\"", contract, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deleted_registry_parser_and_compiler_entrypoints_are_not_in_module_map()
    {
        var root = FindRepositoryRoot();
        var moduleMap = Read(root, "backend/module-file-map.json");

        Assert.DoesNotContain("runtimeRegistry", moduleMap, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DesignerRuntimeRenderer", moduleMap, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("runtimeDocumentCodec", moduleMap, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PageRenderer", moduleMap, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LegacyCompiler", moduleMap, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement ReadAudit(string root)
    {
        using var document = JsonDocument.Parse(Read(root, "docs/low-code-refactor/latest-only-deletion-audit.json"));
        return document.RootElement.Clone();
    }

    private static IReadOnlySet<string> FindProductionFullDesignerDependencies(string root)
    {
        var sourceRoot = Path.Combine(root, "frontend", "AsterERP.Web", "src");
        return EnumerateSourceFiles(sourceRoot)
            .Where(file => !file.Replace('\\', '/').Contains("/full-designer/", StringComparison.OrdinalIgnoreCase))
            .SelectMany(file => File.ReadAllLines(file)
                .Select(line => (file, line))
                .Where(item => item.line.Contains("full-designer", StringComparison.OrdinalIgnoreCase)))
            .Select(item =>
            {
                var relative = Path.GetRelativePath(root, item.file).Replace('\\', '/');
                var target = item.line.Trim();
                return relative + " => " + target;
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateSourceFiles(string path)
    {
        if (File.Exists(path))
        {
            if (IsProductionSourceFile(path)) yield return path;
            yield break;
        }

        if (!Directory.Exists(path))
        {
            throw new InvalidOperationException($"BLOCKED latest-only audit: source path does not exist: {path}");
        }

        foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
        {
            if (IsProductionSourceFile(file)) yield return file;
        }
    }

    private static bool IsProductionSourceFile(string path) =>
        (path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
         path.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) ||
         path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) &&
        !path.EndsWith(".test.ts", StringComparison.OrdinalIgnoreCase) &&
        !path.EndsWith(".test.tsx", StringComparison.OrdinalIgnoreCase) &&
        !path.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase);

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AsterERP.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
