using Xunit;

namespace AsterERP.Api.Tests;

/// <summary>
/// Acceptance guards for the one-time latest-only replacement. Migration input
/// is intentionally allowed to exist, but it must never be a runtime dependency.
/// </summary>
public sealed class LatestOnlyDeletionAcceptanceTests
{
    private static readonly string[] ObsoleteLowCodeFiles =
    {
        "frontend/AsterERP.Web/src/shared/runtime/PageRenderer.tsx",
        "frontend/AsterERP.Web/src/core/ui-engine/ActionRegistry.ts",
        "frontend/AsterERP.Web/src/core/ui-engine/ComponentRegistry.ts",
        "frontend/AsterERP.Web/src/core/ui-engine/SchemaValidator.ts",
        "frontend/AsterERP.Web/src/shared/runtime/designer-document/DesignerRuntimeRenderer.tsx",
        "frontend/AsterERP.Web/src/shared/runtime/designer-document/runtimeDocumentCodec.ts",
        "frontend/AsterERP.Web/src/shared/runtime/designer-document/runtimeDesignerTypes.ts",
        "frontend/AsterERP.Web/src/shared/runtime/designer-document/runtimeBindingValues.ts",
        "frontend/AsterERP.Web/src/shared/runtime/designer-document/runtimeDisplayElements.tsx",
        "frontend/AsterERP.Web/src/shared/runtime/designer-document/runtimeTableEditing.ts",
        "frontend/AsterERP.Web/src/shared/runtime/designer-document/RuntimeVariableTable.tsx",
        "frontend/AsterERP.Web/src/shared/runtime/designer-document/RuntimeTableCellEditor.tsx",
        "frontend/AsterERP.Web/src/shared/runtime/designer-document/pageMicroflowScheduler.ts"
    };

    private static readonly string[] RuntimeChainFiles =
    {
        "frontend/AsterERP.Web/src/pages/runtime/RuntimePage.tsx",
        "frontend/AsterERP.Web/src/runtime-kernel/RuntimeKernel.ts",
        "frontend/AsterERP.Web/src/runtime-kernel/ComponentRuntimeHost.tsx",
        "backend/AsterERP.Api/Application/ApplicationDevelopmentCenter/ApplicationDevelopmentSchemaCompiler.cs",
        "backend/AsterERP.Api/Application/Runtime/RuntimePageSchemaService.cs"
    };

    private static readonly string[] ForbiddenRuntimeTokens =
    {
        "DesignerRuntimeRenderer",
        "PageRenderer",
        "runtimeDocumentCodec",
        "simulatedWidth",
        "UseLegacyRuntime",
        "LegacyRuntimeRenderer",
        "LegacyCompiler",
        "shadowRender",
        "shadowRendering",
        "dualWrite",
        "dualRead",
        "FeatureFlag",
        "featureFlag"
    };

    private static readonly string[] BackendMigrationInputFiles =
    {
        "backend/AsterERP.Api/Application/ApplicationDevelopmentCenter/Migrations/ApplicationDesignerDocumentMigrationService.cs",
        "backend/AsterERP.Api/Application/ApplicationDevelopmentCenter/ApplicationDevelopmentSchemaValidator.cs"
    };

    [Fact]
    public void Obsolete_low_code_entrypoints_and_runtime_modules_are_absent()
    {
        var root = FindRepositoryRoot();

        foreach (var relativePath in ObsoleteLowCodeFiles)
        {
            Assert.False(
                File.Exists(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar))),
                $"Obsolete latest-only entrypoint remains: {relativePath}");
        }
    }

    [Fact]
    public void Runtime_chain_has_no_deleted_renderer_or_legacy_branch()
    {
        var root = FindRepositoryRoot();

        foreach (var relativePath in RuntimeChainFiles)
        {
            var source = Read(root, relativePath);
            foreach (var token in ForbiddenRuntimeTokens)
            {
                Assert.DoesNotContain(token, source, StringComparison.OrdinalIgnoreCase);
            }
        }

        Assert.Contains(
            "ComponentRuntimeHost",
            Read(root, RuntimeChainFiles[0]),
            StringComparison.Ordinal);
        Assert.Contains(
            "[\"renderer\"] = \"designerDocument\"",
            Read(root, RuntimeChainFiles[3]),
            StringComparison.Ordinal);
        Assert.Contains(
            "[\"signature\"] = signature",
            Read(root, RuntimeChainFiles[3]),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_page_schema_service_has_one_preview_entry_and_no_published_preview_fallback()
    {
        var source = Read(FindRepositoryRoot(), "backend/AsterERP.Api/Application/Runtime/RuntimePageSchemaService.cs");

        Assert.Contains("BuildPreviewSchemaAsync", source, StringComparison.Ordinal);
        Assert.Contains("ApplicationDesignerDocumentEntity", source, StringComparison.Ordinal);
        Assert.Contains("DocumentJson", source, StringComparison.Ordinal);
        Assert.Contains("schemaCompiler.CompileSchema", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetPreviewPublishedSchemaAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadPreviewPublishedSchemaAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Backend_migration_inputs_are_present_but_are_not_runtime_chain_dependencies()
    {
        var root = FindRepositoryRoot();

        foreach (var relativePath in BackendMigrationInputFiles)
        {
            Assert.True(File.Exists(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar))),
                $"Required migration input is missing from the audit inventory: {relativePath}");
        }

        Assert.False(File.Exists(Path.Combine(root, "frontend/AsterERP.Web/src/pages/application-console/development-center/low-code-studio/migration/LegacyDocumentInput.ts".Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(File.Exists(Path.Combine(root, "frontend/AsterERP.Web/src/pages/application-console/development-center/low-code-studio/migration/LegacyComponentManifestMigration.ts".Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(File.Exists(Path.Combine(root, "frontend/AsterERP.Web/src/pages/application-console/development-center/low-code-studio/migration/CurrentDocumentMigration.ts".Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(File.Exists(Path.Combine(root, "frontend/AsterERP.Web/src/pages/application-console/development-center/low-code-studio/components/RuntimeActionManifest.ts".Replace('/', Path.DirectorySeparatorChar))));

        foreach (var runtimeFile in RuntimeChainFiles)
        {
            var source = Read(root, runtimeFile);
            Assert.DoesNotContain("parseLegacyDesignerExpressionString", source, StringComparison.Ordinal);
            Assert.DoesNotContain("inferLegacyOperation", source, StringComparison.Ordinal);
            Assert.DoesNotContain("CurrentDocumentMigration", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ApplicationDesignerDocumentMigrationService", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Runtime_page_uses_the_single_latest_kernel_host()
    {
        var root = FindRepositoryRoot();
        var source = Read(root, RuntimeChainFiles[0]);

        Assert.Contains("parseRuntimePageArtifact", source, StringComparison.Ordinal);
        Assert.Contains("toRuntimeArtifact", source, StringComparison.Ordinal);
        Assert.Contains("ComponentRuntimeHost", source, StringComparison.Ordinal);
        Assert.DoesNotContain("renderer ===", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PageRenderer", source, StringComparison.Ordinal);
    }

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
