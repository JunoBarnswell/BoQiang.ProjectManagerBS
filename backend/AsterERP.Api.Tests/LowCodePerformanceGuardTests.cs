using System.Text.Json;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class LowCodePerformanceGuardTests
{
    [Fact]
    public void Performance_baseline_has_machine_readable_budgets_and_evidence_paths()
    {
        var root = FindRepositoryRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs", "low-code-refactor", "performance-baseline.json")));
        var rootElement = document.RootElement;

        Assert.Equal("astererp.low-code.performance-baseline.v1", rootElement.GetProperty("format").GetString());
        var measurementPolicy = rootElement.GetProperty("measurementPolicy");
        Assert.True(measurementPolicy.GetProperty("runs").GetInt32() >= 3);
        Assert.True(measurementPolicy.GetProperty("passRequiresRawEvidence").GetBoolean());

        var scenarios = rootElement.GetProperty("scenarios").EnumerateArray().ToArray();
        Assert.NotEmpty(scenarios);
        foreach (var scenario in scenarios)
        {
            Assert.False(string.IsNullOrWhiteSpace(scenario.GetProperty("id").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(scenario.GetProperty("kind").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(scenario.GetProperty("evidencePath").GetString()));
            var status = scenario.GetProperty("status").GetString();
            Assert.Contains(status, new[] { "PendingExecution", "Blocked", "Measured" });
            if (status == "Blocked")
            {
                Assert.False(string.IsNullOrWhiteSpace(scenario.GetProperty("blockedReason").GetString()));
            }
        }
    }

    [Fact]
    public void Runtime_entry_uses_bounded_cache_and_cancellation_contract()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root, "frontend", "AsterERP.Web", "src", "pages", "runtime", "RuntimePage.tsx"));
        var host = File.ReadAllText(Path.Combine(root, "frontend", "AsterERP.Web", "src", "runtime-kernel", "ComponentRuntimeHost.tsx"));

        Assert.Contains("staleTimeMs", page, StringComparison.Ordinal);
        Assert.Contains("PREVIEW_PAGE_SCHEMA_STALE_TIME_MS", page, StringComparison.Ordinal);
        Assert.Contains("signal })", page, StringComparison.Ordinal);
        Assert.Contains("new AbortController()", host, StringComparison.Ordinal);
        Assert.Contains("runtimeAbortController.signal", host, StringComparison.Ordinal);
        Assert.Contains("abort('Runtime host unmounted.')", host, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AsterERP.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("BLOCKED quality guard: AsterERP.sln was not found.");
    }
}
