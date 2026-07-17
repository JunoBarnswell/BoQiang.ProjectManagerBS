using Xunit;

namespace AsterERP.Api.Tests;

public sealed class LowCodeAccessibilityGuardTests
{
    [Fact]
    public void Runtime_page_has_distinct_loading_error_and_authorization_states()
    {
        var source = ReadProductionSource("frontend/AsterERP.Web/src/pages/runtime/RuntimePage.tsx");
        Assert.Contains("<PageLoading", source, StringComparison.Ordinal);
        Assert.Contains("<PageError", source, StringComparison.Ordinal);
        Assert.Contains("<Page403", source, StringComparison.Ordinal);
        Assert.Contains("<Page404", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_diagnostic_state_is_exposed_as_an_alert_region()
    {
        var source = ReadProductionSource("frontend/AsterERP.Web/src/runtime-kernel/ComponentRuntimeHost.tsx");
        Assert.Contains("role=\"alert\"", source, StringComparison.Ordinal);
        Assert.Contains("Runtime artifact is being verified.", source, StringComparison.Ordinal);
        Assert.Contains("Runtime artifact has no root element.", source, StringComparison.Ordinal);
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
