using System.Security.Claims;
using System.Text;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementReportTests
{
    [Fact]
    public void Report_controller_requires_export_permission_on_both_formats()
    {
        var controller = typeof(ProjectManagementReportsController);
        var classPermission = Assert.Single(controller.GetCustomAttributes(typeof(PermissionAttribute), true));
        Assert.Equal(PermissionCodes.ProjectManagementReportExport, ((PermissionAttribute)classPermission).Code);
        Assert.Contains(controller.GetMethods(), method => method.Name == nameof(ProjectManagementReportsController.ExportCsvAsync));
        Assert.Contains(controller.GetMethods(), method => method.Name == nameof(ProjectManagementReportsController.ExportExcelAsync));
    }

    [Fact]
    public async Task Report_exports_only_current_workspace_and_sanitizes_formula_values()
    {
        using var db = CreateDatabase("report-isolation");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new[]
        {
            new ProjectManagementProjectEntity { Id = "visible", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "=VISIBLE", ProjectName = "Visible", OwnerUserId = "operator" },
            new ProjectManagementProjectEntity { Id = "other-tenant", TenantId = "tenant-b", AppCode = "SYSTEM", ProjectCode = "OTHER", ProjectName = "Other tenant", OwnerUserId = "operator" },
            new ProjectManagementProjectEntity { Id = "other-app", TenantId = "tenant-a", AppCode = "CRM", ProjectCode = "OTHER-APP", ProjectName = "Other app", OwnerUserId = "operator" }
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "visible", TaskCode = "T-001", Title = "Task" }).ExecuteCommandAsync();

        var user = CreateUser("operator", "tenant-a", "SYSTEM");
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementProjectEntity), user, "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskEntity), user, "tenant-a", "SYSTEM"));
        var service = new ProjectManagementReportService(new TestWorkspaceDatabaseAccessor(db), user);
        var csv = await service.ExportCsvAsync(new ProjectManagementReportQuery(PageSize: 500));
        var csvText = Encoding.UTF8.GetString(csv.Content);

        Assert.Equal(1, csv.RowCount);
        Assert.Contains("'=VISIBLE", csvText);
        Assert.Contains("\"1\"", csvText);
        Assert.DoesNotContain("Other tenant", csvText);
        Assert.DoesNotContain("Other app", csvText);

        var excel = await service.ExportExcelAsync(new ProjectManagementReportQuery(PageSize: 500));
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(excel.Content));
        Assert.Equal("'=VISIBLE", workbook.Worksheet("ProjectReport").Cell(2, 1).GetString());
    }

    [Fact]
    public async Task Report_clamps_page_size_and_returns_valid_empty_exports()
    {
        using var db = CreateDatabase("report-empty");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var service = new ProjectManagementReportService(new TestWorkspaceDatabaseAccessor(db), CreateUser("operator", "tenant-a", "MES"));

        var csv = await service.ExportCsvAsync(new ProjectManagementReportQuery(PageIndex: 1, PageSize: 10000));
        var csvText = Encoding.UTF8.GetString(csv.Content);
        Assert.Equal(0, csv.RowCount);
        Assert.Contains("ProjectCode", csvText);
        Assert.Contains("ProjectName", csvText);
    }

    [Fact]
    public async Task Report_requires_tenant_and_app_context()
    {
        using var db = CreateDatabase("report-context");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var service = new ProjectManagementReportService(new TestWorkspaceDatabaseAccessor(db), CreateUser("operator", null, "MES"));

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.ExportCsvAsync(new ProjectManagementReportQuery()));
    }

    private static SqlSugarClient CreateDatabase(string name) => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:project-management-{name}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });

    private static FixedAsterErpCurrentUser CreateUser(string userId, string? tenantId, string appCode) =>
        new(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AsterErpClaimTypes.UserId, userId),
            tenantId is null ? new Claim(AsterErpClaimTypes.AppCode, appCode) : new Claim(AsterErpClaimTypes.TenantId, tenantId),
            new Claim(AsterErpClaimTypes.AppCode, appCode),
            new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.ProjectManagementReportExport)
        }, "test")));

    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }
}
