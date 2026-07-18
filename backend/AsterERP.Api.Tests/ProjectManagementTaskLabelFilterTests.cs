using System.Security.Claims;
using System.Text;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementTaskLabelFilterTests
{
    [Fact]
    public async Task Label_filter_uses_one_database_protocol_for_task_views_and_exports()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-label-filter-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "task-red", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "RED", Title = "Red", Status = "Todo", Priority = "Medium", CreatedBy = "operator", CreatedTime = DateTime.UtcNow },
            new ProjectManagementTaskEntity { Id = "task-blue", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "BLUE", Title = "Blue", Status = "Todo", Priority = "Medium", CreatedBy = "operator", CreatedTime = DateTime.UtcNow },
            new ProjectManagementTaskEntity { Id = "task-both", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "BOTH", Title = "Both", Status = "Todo", Priority = "Medium", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementLabelEntity { Id = "label-red", TenantId = "tenant-a", AppCode = "SYSTEM", LabelName = "Red", Color = "#FF0000", CreatedBy = "operator", CreatedTime = DateTime.UtcNow },
            new ProjectManagementLabelEntity { Id = "label-blue", TenantId = "tenant-a", AppCode = "SYSTEM", LabelName = "Blue", Color = "#0000FF", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            Link("task-red", "label-red"), Link("task-blue", "label-blue"), Link("task-both", "label-red"), Link("task-both", "label-blue")
        }).ExecuteCommandAsync();

        var user = CreateUser();
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var taskService = new ProjectManagementTaskService(accessor, user);
        var reportService = new ProjectManagementReportService(accessor, user);
        var anyRed = new ProjectManagementTaskLabelFilter(["label-red"]);
        var allLabels = new ProjectManagementTaskLabelFilter(["label-red", "label-blue"], ProjectManagementTaskLabelMatchModes.All);

        foreach (var view in new[] { "list", "board", "gantt" })
        {
            var page = await taskService.QueryAsync(new ProjectManagementTaskQuery("project-a", ViewKey: view, SortBy: view == "gantt" ? "dueDate" : "tree", LabelFilter: anyRed));
            Assert.Equal(["task-both", "task-red"], page.Items.Select(item => item.Id).OrderBy(id => id).ToArray());
        }

        var allPage = await taskService.QueryAsync(new ProjectManagementTaskQuery("project-a", LabelFilter: allLabels));
        Assert.Equal("task-both", Assert.Single(allPage.Items).Id);

        var anyExport = await reportService.ExportCsvAsync(new ProjectManagementReportQuery(LabelFilter: anyRed));
        var allExport = await reportService.ExportCsvAsync(new ProjectManagementReportQuery(LabelFilter: allLabels));
        Assert.Equal(1, anyExport.RowCount);
        Assert.Equal(1, allExport.RowCount);
        Assert.Contains("\"2\"", Encoding.UTF8.GetString(anyExport.Content));
        Assert.Contains("\"1\"", Encoding.UTF8.GetString(allExport.Content));
    }

    private static ProjectManagementTaskLabelEntity Link(string taskId, string labelId) => new()
    {
        TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = taskId, LabelId = labelId, CreatedBy = "operator", CreatedTime = DateTime.UtcNow
    };

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
