using System.Text.Json;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class LatestOnlyDeletionAuditTests
{
    [Fact]
    public void Audit_record_is_present_and_confirms_no_production_legacy_couplings()
    {
        var auditPath = Path.Combine(FindRepositoryRoot(), "docs", "low-code-refactor", "latest-only-deletion-audit.json");
        using var document = JsonDocument.Parse(File.ReadAllText(auditPath));
        var root = document.RootElement;

        Assert.Equal("latest-only", root.GetProperty("semanticPolicy").GetString());
        Assert.Empty(root.GetProperty("blockers").EnumerateArray());
        Assert.Empty(root.GetProperty("productionDependencies").EnumerateArray());
        Assert.True(root.GetProperty("deletedEntries").GetArrayLength() >= 3);
        Assert.True(root.GetProperty("externalEvidence").GetArrayLength() >= 4);
    }

    [Fact]
    public void Deleted_latest_only_entries_are_absent_from_the_worktree()
    {
        var root = FindRepositoryRoot();
        var deletedPaths = new[]
        {
            "frontend/AsterERP.Web/src/shared/runtime/PageRenderer.tsx",
            "frontend/AsterERP.Web/src/core/ui-engine/ActionRegistry.ts",
            "frontend/AsterERP.Web/src/core/ui-engine/ComponentRegistry.ts",
            "frontend/AsterERP.Web/src/core/ui-engine/SchemaValidator.ts",
            "frontend/AsterERP.Web/src/shared/runtime/designer-document/DesignerRuntimeRenderer.tsx",
            "frontend/AsterERP.Web/src/shared/runtime/designer-document/runtimeDocumentCodec.ts",
            "docs/contracts/designer-document-v3.schema.json"
        };

        foreach (var relativePath in deletedPaths)
        {
            Assert.False(File.Exists(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar))), relativePath);
        }
    }

    [Fact]
    public void Audit_record_points_to_existing_latest_contract_and_runtime_kernel()
    {
        var root = FindRepositoryRoot();
        Assert.True(File.Exists(Path.Combine(root, "docs", "contracts", "designer-document.latest.schema.json")));
        Assert.True(File.Exists(Path.Combine(root, "frontend", "AsterERP.Web", "src", "runtime-kernel", "RuntimeKernel.ts")));
        Assert.True(File.Exists(Path.Combine(root, "frontend", "AsterERP.Web", "src", "runtime-kernel", "ComponentRuntimeHost.tsx")));
    }

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
