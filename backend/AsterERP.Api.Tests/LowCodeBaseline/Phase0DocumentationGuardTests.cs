using System.Text.Json;
using Xunit;

namespace AsterERP.Api.Tests.LowCodeBaseline;

public sealed class Phase0DocumentationGuardTests
{
    [Fact]
    public void Phase0_runners_are_present_and_fail_closed()
    {
        var root = RepositoryRoot();
        var control = Read(root, "docs/low-code-refactor/phase0-control.ps1");
        var performance = Read(root, "docs/low-code-refactor/phase0-performance.ps1");
        foreach (var action in new[] { "CaptureSnapshot", "NewEvidence", "ValidateEvidence", "HealthCheck", "RestoreSnapshot", "CaptureBaseline" }) Assert.Contains(action, control);
        foreach (var action in new[] { "ValidatePlan", "RunParseValidate", "PrepareWideTable" }) Assert.Contains(action, performance);
        Assert.Contains("AllowRestore", control);
        Assert.Contains("blockedReason", performance);
        Assert.DoesNotContain("class Bridge", control);
        Assert.DoesNotContain("class Adapter", control);
        Assert.DoesNotContain("class Facade", control);
    }

    [Fact]
    public void Phase0_assets_define_auditable_snapshot_health_rollback_and_provider_contracts()
    {
        var root = RepositoryRoot();
        using var evidence = JsonDocument.Parse(Read(root, "docs/low-code-refactor/migration-evidence.schema.json"));
        using var providers = JsonDocument.Parse(Read(root, "docs/low-code-refactor/database-provider-capability-matrix.json"));
        using var performance = JsonDocument.Parse(Read(root, "docs/low-code-refactor/performance-baseline.json"));
        var required = evidence.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToHashSet();
        foreach (var field in new[] { "maintenanceLockId", "backupPath", "backupSha256", "sourceCommit", "targetCommit", "previousArtifactId", "healthCheckId", "traceId" }) Assert.Contains(field, required);
        Assert.Equal(4, providers.RootElement.GetProperty("providers").GetArrayLength());
        foreach (var provider in providers.RootElement.GetProperty("providers").EnumerateArray()) Assert.Equal("required", provider.GetProperty("queryCancel").GetString());
        Assert.Equal(5, performance.RootElement.GetProperty("measurementPolicy").GetProperty("runs").GetInt32());
        Assert.Contains("wide-table-browse", performance.RootElement.GetProperty("scenarios").EnumerateArray().Select(item => item.GetProperty("id").GetString()));
    }

    [Fact]
    public void Fixed_database_source_and_runbook_controls_are_recorded()
    {
        var root = RepositoryRoot();
        using var baseline = JsonDocument.Parse(Read(root, "docs/low-code-refactor/source-and-database-baseline.json"));
        var runbook = Read(root, "docs/low-code-refactor/migration-and-rollback-runbook.md");
        var databasePath = baseline.RootElement.GetProperty("debugDatabase").GetProperty("path").GetString();
        Assert.Contains("application-databases/tenant-a/MES/mes11.db", databasePath!.Replace("\\", "/"), StringComparison.OrdinalIgnoreCase);
        foreach (var phrase in new[] { "CaptureSnapshot", "HealthCheck", "RestoreSnapshot", "previousArtifactId", "provider", "Pass", "Fail", "Blocked" }) Assert.Contains(phrase, runbook, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string root, string relativePath) => File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AsterERP.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("AsterERP.sln was not found");
    }
}
