using Xunit;

namespace AsterERP.Api.Tests;

public sealed class RuntimeLatestOnlyGuardTests
{
    [Fact]
    public void Runtime_page_has_no_legacy_renderer_fallback()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "frontend", "AsterERP.Web", "src", "pages", "runtime", "RuntimePage.tsx"));

        Assert.DoesNotContain("PageRenderer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("renderer ===", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_compiler_emits_the_single_artifact_renderer_and_signature()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "backend", "AsterERP.Api", "Application", "ApplicationDevelopmentCenter", "ApplicationDevelopmentSchemaCompiler.cs"));

        Assert.Contains("[\"renderer\"] = \"designerDocument\"", source, StringComparison.Ordinal);
        Assert.Contains("[\"signature\"] = signature", source, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"renderer\"] = string.IsNullOrWhiteSpace(modelCode)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Obsolete_generic_runtime_entry_and_registries_are_deleted()
    {
        var repositoryRoot = FindRepositoryRoot();
        var obsoleteFiles = new[]
        {
            Path.Combine(repositoryRoot, "frontend", "AsterERP.Web", "src", "shared", "runtime", "PageRenderer.tsx"),
            Path.Combine(repositoryRoot, "frontend", "AsterERP.Web", "src", "core", "ui-engine", "ActionRegistry.ts"),
            Path.Combine(repositoryRoot, "frontend", "AsterERP.Web", "src", "core", "ui-engine", "ComponentRegistry.ts"),
            Path.Combine(repositoryRoot, "frontend", "AsterERP.Web", "src", "core", "ui-engine", "SchemaValidator.ts")
        };

        foreach (var obsoleteFile in obsoleteFiles)
        {
            Assert.False(File.Exists(obsoleteFile), $"Obsolete generic runtime entry remains: {obsoleteFile}");
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AsterERP.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
