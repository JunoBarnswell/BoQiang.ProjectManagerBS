using System.Text.Json;
using AsterERP.Api.Controllers;
using AsterERP.Contracts.ApplicationConsole;
using AsterERP.Contracts.Auth;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationLoginBootstrapResponseTests
{
    [Fact]
    public void Anonymous_bootstrap_omits_database_binding_details_when_values_are_null()
    {
        var response = new ApplicationLoginBootstrapResponse(
            "tenant-a",
            "客户A",
            "MES",
            "制造执行系统",
            "客户A MES",
            "Enabled",
            new ApplicationDatabaseBindingStatusResponse(
                IsBound: true,
                IsReachable: false,
                Provider: null,
                DisplayName: null,
                DatabaseName: null,
                UpdatedAt: null,
                CanManage: false,
                Message: null));

        AssertDatabaseDiagnosticsOmitted(response);
    }

    [Fact]
    public void Bootstrap_controller_redacts_database_diagnostics_before_serialization()
    {
        var response = new ApplicationLoginBootstrapResponse(
            "tenant-a",
            "客户A",
            "MES",
            "制造执行系统",
            "客户A MES",
            "Enabled",
            new ApplicationDatabaseBindingStatusResponse(
                IsBound: true,
                IsReachable: false,
                Provider: "SqlServer",
                DisplayName: "production-db.internal:1433",
                DatabaseName: "astererp_mes",
                UpdatedAt: DateTime.UtcNow,
                CanManage: false,
                Message: "Login failed for user sa on production-db.internal"));

        var redacted = ApplicationAuthController.RedactBootstrapDiagnostics(response);

        Assert.True(redacted.DatabaseBinding.IsBound);
        Assert.False(redacted.DatabaseBinding.IsReachable);
        Assert.False(redacted.DatabaseBinding.CanManage);
        AssertDatabaseDiagnosticsOmitted(redacted);
    }

    private static void AssertDatabaseDiagnosticsOmitted(ApplicationLoginBootstrapResponse response)
    {
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.DoesNotContain("\"provider\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"displayName\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"databaseName\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"updatedAt\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"message\"", json, StringComparison.OrdinalIgnoreCase);
    }
}
