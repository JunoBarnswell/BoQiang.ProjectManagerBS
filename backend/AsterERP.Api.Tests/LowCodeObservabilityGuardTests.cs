using Xunit;

namespace AsterERP.Api.Tests;

public sealed class LowCodeObservabilityGuardTests
{
    [Fact]
    public void Api_pipeline_registers_request_diagnostics_operation_logging_and_trace_context()
    {
        var source = ReadProductionSource("backend/AsterERP.Api/Program.cs");
        Assert.Contains("RequestDiagnosticsMiddleware", source, StringComparison.Ordinal);
        Assert.Contains("OperationLogMiddleware", source, StringComparison.Ordinal);
        Assert.Contains("UseSerilogRequestLogging", source, StringComparison.Ordinal);
        Assert.Contains("TraceId", source, StringComparison.Ordinal);
        Assert.Contains("httpContext.TraceIdentifier", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Quality_gate_documents_pass_fail_and_blocked_outcomes()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "low-code-refactor", "quality-gates.md"));
        Assert.Contains("Pass", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Fail", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Blocked", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet test", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("npm run", source, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadProductionSource(string relativePath) => File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

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
