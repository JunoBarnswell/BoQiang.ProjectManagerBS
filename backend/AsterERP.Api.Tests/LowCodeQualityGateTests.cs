using System.Text.Json;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class LowCodeQualityGateTests
{
    private static readonly string[] RequiredWorkflowTokens =
    [
        "scan-latest-only.ps1",
        "LatestOnlySourceScanGuardTests",
        "LatestOnlyDeletionAcceptanceTests",
        "ApplicationDevelopmentMigrationTests",
        "RuntimePageSchemaServiceTests",
        "RuntimeArtifactIntegrity.test.ts",
        "goldenCases.test.ts",
        "ApplicationDataStudioSqliteIntegrationTests",
        "ApplicationDataSourceExternalProviderGateTests",
        "runtimeSecurityPolicy.test.ts",
        "RuntimeMonitoringContract.test.ts",
        "pageStudioHao111Acceptance.test.ts",
        "pageStudioHao112Acceptance.test.ts",
        "pageStudioHao113Acceptance.test.ts",
        "low-code-external",
        "external-evidence-contract.json"
    ];

    private static readonly string[] RequiredQualityDocumentTokens =
    [
        "Latest-only deletion tests",
        "Designer migration/source boundary",
        "Runtime artifact contract",
        "Data Studio local chain",
        "External provider preflight",
        "Performance evidence",
        "Blocked and EvidencePresent must never be converted to Pass"
    ];

    [Fact]
    public void Quality_gate_workflow_covers_latest_migration_runtime_data_and_external_boundaries()
    {
        var root = FindRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "low-code-quality-gates.yml"));
        var missing = RequiredWorkflowTokens.Where(token => !workflow.Contains(token, StringComparison.OrdinalIgnoreCase)).ToArray();

        Assert.True(missing.Length == 0, "CI quality workflow is missing required gate coverage: " + string.Join(", ", missing));
    }

    [Fact]
    public void Quality_gate_document_lists_local_and_external_status_boundaries()
    {
        var root = FindRepositoryRoot();
        var document = File.ReadAllText(Path.Combine(root, "docs", "low-code-refactor", "quality-gates.md"));
        var missing = RequiredQualityDocumentTokens.Where(token => !document.Contains(token, StringComparison.OrdinalIgnoreCase)).ToArray();

        Assert.True(missing.Length == 0, "Quality-gates documentation is missing required boundaries: " + string.Join(", ", missing));
    }

    [Fact]
    public void Acceptance_matrix_never_promotes_blocked_external_evidence_to_release_pass()
    {
        var root = FindRepositoryRoot();
        var matrixPath = Path.Combine(root, "docs", "low-code-refactor", "hao-105-110-acceptance-matrix.json");
        using var matrix = JsonDocument.Parse(File.ReadAllText(matrixPath));
        var allowedStatuses = matrix.RootElement.GetProperty("statusVocabulary")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var issues = matrix.RootElement.GetProperty("issues").EnumerateArray().ToArray();
        var invalidStatuses = issues
            .SelectMany(issue => new[] { "localStatus", "externalStatus", "releaseStatus" }
                .Select(field => new { Id = issue.GetProperty("id").GetString() ?? "unknown", Field = field, Value = issue.GetProperty(field).GetString() }))
            .Where(item => item.Value is null || !allowedStatuses.Contains(item.Value))
            .Select(item => $"{item.Id}.{item.Field}={item.Value ?? "<null>"}")
            .ToArray();
        var violations = issues
            .Where(issue => string.Equals(issue.GetProperty("releaseStatus").GetString(), "Pass", StringComparison.OrdinalIgnoreCase))
            .Where(issue => !string.Equals(issue.GetProperty("localStatus").GetString(), "Pass", StringComparison.OrdinalIgnoreCase) ||
                            !string.Equals(issue.GetProperty("externalStatus").GetString(), "Pass", StringComparison.OrdinalIgnoreCase))
            .Select(issue => issue.GetProperty("id").GetString() ?? "unknown")
            .ToArray();

        Assert.Empty(invalidStatuses);
        Assert.Empty(violations);
        Assert.True(matrix.RootElement.GetProperty("policy").GetProperty("blockedNeverUpgradesToPass").GetBoolean());
        Assert.True(matrix.RootElement.GetProperty("policy").GetProperty("localPassIsNotReleasePass").GetBoolean());
    }

    [Fact]
    public void Quality_workflow_requires_each_external_evidence_boundary_and_not_an_arbitrary_pass_file()
    {
        var root = FindRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "low-code-quality-gates.yml"));
        var contractPath = Path.Combine(root, "docs", "low-code-refactor", "external-evidence-contract.json");
        using var contract = JsonDocument.Parse(File.ReadAllText(contractPath));
        var requiredFiles = contract.RootElement.GetProperty("requiredFiles").EnumerateArray().ToArray();
        var requiredFields = contract.RootElement.GetProperty("requiredFields").EnumerateArray().ToArray();
        var requiredTokens = new[] { "external-evidence-contract.json", "requiredEvidenceFiles", "requiredFields" }
            .ToArray();
        var missing = requiredTokens
            .Where(file => !workflow.Contains(file, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(requiredFiles);
        Assert.NotEmpty(requiredFields);
        Assert.Equal("Pass", contract.RootElement.GetProperty("passRules").GetProperty("statusMustEqual").GetString());
        Assert.True(contract.RootElement.GetProperty("passRules").GetProperty("blockedReasonMustBeEmpty").GetBoolean());
        Assert.Empty(missing);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AsterERP.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("BLOCKED quality gate guard: AsterERP.sln was not found.");
    }
}
