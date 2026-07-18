using System.Security.Claims;
using System.Text;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementTaskCsvExportTests
{
    [Fact]
    public async Task Task_csv_export_reuses_task_filters_permissions_and_chunked_pages()
    {
        using var db = CreateDatabase();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "P-A", ProjectName = "Project A", OwnerUserId = "operator"
        }).ExecuteCommandAsync();

        var tasks = Enumerable.Range(1, 201).Select(index => new ProjectManagementTaskEntity
        {
            Id = $"task-{index:000}", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a",
            TaskCode = index == 1 ? "=FORMULA" : $"T-{index:000}",
            Title = index == 2 ? "line\r\n\"quoted\"" : $"Task {index}",
            Summary = "Summary", Status = "Todo", Priority = "Medium", SortOrder = index,
            CreatedBy = "operator", CreatedTime = DateTime.UtcNow
        }).ToArray();
        await db.Insertable(tasks).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity
        {
            Id = "task-other-tenant", TenantId = "tenant-b", AppCode = "SYSTEM", ProjectId = "project-a",
            TaskCode = "OTHER", Title = "Other tenant", Status = "Todo", Priority = "Medium", CreatedBy = "other", CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();

        var user = CreateUser();
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementProjectEntity), user, "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskEntity), user, "tenant-a", "SYSTEM"));
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var taskService = new ProjectManagementTaskService(accessor, user);
        var reportService = new ProjectManagementReportService(accessor, user, taskService: taskService);

        var file = await reportService.ExportTasksCsvAsync(new ProjectManagementTaskQuery("project-a", PageSize: 1, Status: "Todo", ViewKey: "list"));
        var csv = Encoding.UTF8.GetString(file.Content);

        Assert.Equal(201, file.RowCount);
        Assert.True(file.Content.AsSpan(0, 3).SequenceEqual(Encoding.UTF8.GetPreamble()));
        Assert.Matches("^project-management-tasks-projecta-[0-9]{14}\\.csv$", file.FileName);
        Assert.Contains("'=FORMULA", csv);
        Assert.Contains("\"line\r\n\"\"quoted\"\"\"", csv);
        Assert.DoesNotContain("Other tenant", csv);
    }

    private static SqlSugarClient CreateDatabase() => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:project-management-task-csv-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, "operator"),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"),
        new Claim(AsterErpClaimTypes.DataScope, "SELF")
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
