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
        await db.Insertable(new ProjectManagementTaskEntity { TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "visible", TaskCode = "T-001", Title = "Task", EstimateMinutes = 120, ActualMinutes = 45 }).ExecuteCommandAsync();

        var user = CreateUser("operator", "tenant-a", "SYSTEM");
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementProjectEntity), user, "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskEntity), user, "tenant-a", "SYSTEM"));
        var service = new ProjectManagementReportService(new TestWorkspaceDatabaseAccessor(db), user);
        var csv = await service.ExportCsvAsync(new ProjectManagementReportQuery(PageSize: 500));
        var csvText = Encoding.UTF8.GetString(csv.Content);

        Assert.Equal(1, csv.RowCount);
        Assert.Contains("'=VISIBLE", csvText);
        Assert.Contains("\"1\"", csvText);
        Assert.Contains("\"EstimatedMinutes\"", csvText);
        Assert.Contains("\"ActualMinutes\"", csvText);
        Assert.Contains("\"120\",\"45\"", csvText);
        Assert.DoesNotContain("Other tenant", csvText);
        Assert.DoesNotContain("Other app", csvText);

        var excel = await service.ExportExcelAsync(new ProjectManagementReportQuery(PageSize: 500));
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(excel.Content));
        Assert.Equal("'=VISIBLE", workbook.Worksheet("ProjectReport").Cell(2, 1).GetString());
        Assert.Equal("ProjectManagement.ExcelSnapshot", workbook.Worksheet("Schema").Cell(2, 2).GetString());
        Assert.Equal("visible", workbook.Worksheet("Projects").Cell(2, 1).GetString());
        Assert.Equal("Visible", workbook.Worksheet("Tasks").Cell(2, 3).GetString());
        Assert.Equal("Task", workbook.Worksheet("Tasks").Cell(2, 9).GetString());
        foreach (var worksheet in new[] { "Milestones", "Tasks", "ProjectMembers", "Participants", "Tags", "TaskTags", "Dependencies", "Comments", "ProgressLogs", "Attachments", "Reminders", "Activities", "ChangeJournal" })
            Assert.NotNull(workbook.Worksheet(worksheet));
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

    [Fact]
    public async Task Markdown_export_filters_tasks_by_task_ids()
    {
        using var db = CreateDatabase("report-markdown-filter");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a",
            TenantId = "tenant-a",
            AppCode = "SYSTEM",
            ProjectCode = "PRJ-001",
            ProjectName = "Demo Project",
            OwnerUserId = "operator",
            Status = "Active",
            Priority = "Medium",
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-001", Title = "Alpha Task", Status = "Todo" },
            new ProjectManagementTaskEntity { Id = "task-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-002", Title = "Beta Task", Status = "InProgress" },
        }).ExecuteCommandAsync();

        var user = CreateUser("operator", "tenant-a", "SYSTEM");
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementProjectEntity), user, "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskEntity), user, "tenant-a", "SYSTEM"));
        var service = new ProjectManagementReportService(new TestWorkspaceDatabaseAccessor(db), user);

        var markdown = DecodeMarkdown(await service.ExportProjectMarkdownAsync("project-a", new ProjectManagementProjectMarkdownOptions(TaskIds: "task-a")));

        Assert.Contains("Alpha Task", markdown);
        Assert.DoesNotContain("Beta Task", markdown);
        Assert.Equal(1, (await service.ExportProjectMarkdownAsync("project-a", new ProjectManagementProjectMarkdownOptions(TaskIds: "task-a"))).RowCount);
    }

    [Fact]
    public async Task Markdown_export_can_omit_project_info_but_keep_timeline()
    {
        using var db = CreateDatabase("report-markdown-project-info");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a",
            TenantId = "tenant-a",
            AppCode = "SYSTEM",
            ProjectCode = "PRJ-001",
            ProjectName = "Demo Project",
            OwnerUserId = "operator",
            Status = "Active",
            Priority = "Medium",
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity
        {
            Id = "task-a",
            TenantId = "tenant-a",
            AppCode = "SYSTEM",
            ProjectId = "project-a",
            TaskCode = "T-001",
            Title = "Alpha Task",
            Status = "Todo",
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementActivityEntity
        {
            Id = "activity-a",
            TenantId = "tenant-a",
            AppCode = "SYSTEM",
            ProjectId = "project-a",
            AggregateType = "Project",
            AggregateId = "project-a",
            ActivityType = "task.created",
            Summary = "Created task",
            TraceId = "trace-a",
            ActorUserId = "operator",
        }).ExecuteCommandAsync();

        var user = CreateUser("operator", "tenant-a", "SYSTEM");
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementProjectEntity), user, "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskEntity), user, "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementActivityEntity), user, "tenant-a", "SYSTEM"));
        var service = new ProjectManagementReportService(new TestWorkspaceDatabaseAccessor(db), user);

        var markdown = DecodeMarkdown(await service.ExportProjectMarkdownAsync("project-a", new ProjectManagementProjectMarkdownOptions(IncludeProjectInfo: false)));

        Assert.DoesNotContain("项目编号", markdown);
        Assert.DoesNotContain("## 概览", markdown);
        Assert.DoesNotContain("## 里程碑", markdown);
        Assert.Contains("## 任务进展", markdown);
        Assert.Contains("Alpha Task", markdown);
        Assert.Contains("## 动态时间线", markdown);
        Assert.Contains("Created task", markdown);
    }

    private static string DecodeMarkdown(ProjectManagementReportFile file)
    {
        var content = file.Content;
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
            content = content[3..];
        return Encoding.UTF8.GetString(content);
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
