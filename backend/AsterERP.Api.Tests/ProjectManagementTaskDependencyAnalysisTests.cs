using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementTaskDependencyAnalysisTests
{
    private static readonly DateTime Day1 = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Critical_path_and_total_float_are_stable_for_parallel_finish_to_start_graph()
    {
        var result = ProjectManagementTaskDependencyAnalysisCalculator.Calculate(
        [
            new("a", "A", null, Day1, Day1.AddDays(1)),
            new("b", "B", null, Day1, Day1.AddDays(2)),
            new("c", "C", null, Day1, Day1.AddDays(1))
        ],
        [
            new("a-c", "a", "c", "FinishToStart", 0),
            new("b-c", "b", "c", "FinishToStart", 0)
        ], []);

        var a = Assert.Single(result.Tasks, item => item.TaskId == "a");
        var b = Assert.Single(result.Tasks, item => item.TaskId == "b");
        var c = Assert.Single(result.Tasks, item => item.TaskId == "c");
        Assert.Equal(1440, a.TotalFloatMinutes);
        Assert.False(a.IsCritical);
        Assert.True(b.IsCritical);
        Assert.True(c.IsCritical);
        Assert.Equal(Day1.AddDays(3), result.ProjectEarliestFinish);
        Assert.True(Assert.Single(result.Links, item => item.DependencyId == "b-c").IsCritical);
        Assert.False(Assert.Single(result.Links, item => item.DependencyId == "a-c").IsCritical);
    }

    [Fact]
    public void Cycle_missing_dates_and_deleted_endpoints_have_diagnostics_without_throwing()
    {
        var result = ProjectManagementTaskDependencyAnalysisCalculator.Calculate(
        [
            new("a", "A", null, Day1, Day1.AddDays(1)),
            new("b", "B", null, Day1, Day1.AddDays(1)),
            new("missing-date", "Missing", null, Day1, null)
        ],
        [
            new("a-b", "a", "b", "FinishToStart", 0),
            new("b-a", "b", "a", "FinishToStart", 0),
            new("ghost-b", "ghost", "b", "FinishToStart", 0)
        ], []);

        Assert.Contains(result.Diagnostics, item => item.Code == "DependencyCycle");
        Assert.Contains(result.Diagnostics, item => item.Code == "MissingScheduleDate");
        Assert.Contains(result.Diagnostics, item => item.Code == "DeletedOrInaccessibleTask");
        Assert.False(Assert.Single(result.Tasks, item => item.TaskId == "a").IsSchedulable);
        Assert.False(Assert.Single(result.Links, item => item.DependencyId == "ghost-b").IsRenderable);
    }

    [Fact]
    public async Task Impact_preview_returns_manual_only_successor_suggestions_and_milestone_risk()
    {
        using var db = await CreateDbAsync();
        await db.Insertable(new[]
        {
            NewTask("a", null, Day1, Day1.AddDays(1)),
            NewTask("b", "m1", Day1, Day1.AddDays(1))
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskDependencyEntity
        {
            Id = "a-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", PredecessorTaskId = "a", SuccessorTaskId = "b", DependencyType = "FinishToStart", CreatedTime = Day1
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementMilestoneEntity
        {
            Id = "m1", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", MilestoneName = "Release", DueDate = Day1.AddDays(2)
        }).ExecuteCommandAsync();
        var user = CreateUser();
        var service = new ProjectManagementTaskDependencyAnalysisService(new TestWorkspaceDatabaseAccessor(db), user, new ProjectManagementAccessPolicy(new TestWorkspaceDatabaseAccessor(db), user));

        var preview = await service.PreviewImpactAsync("project-a", new ProjectManagementTaskDependencyImpactPreviewRequest("a", Day1.AddDays(2), Day1.AddDays(3)));

        var suggestion = Assert.Single(preview.Suggestions, item => item.TaskId == "b");
        Assert.Equal(Day1.AddDays(1), suggestion.CurrentStart);
        Assert.Equal(Day1.AddDays(3), suggestion.SuggestedStart);
        Assert.True(suggestion.RequiresManualConfirmation);
        var impact = Assert.Single(preview.Preview.MilestoneImpacts);
        Assert.True(impact.IsAtRisk);
        Assert.Equal(2 * 1440, impact.DelayMinutes);
    }

    [Fact]
    public void Analysis_controller_exposes_view_protected_analysis_and_preview_endpoints()
    {
        Assert.Contains(typeof(ProjectManagementTaskDependencyAnalysisController).GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskView);
        Assert.NotNull(typeof(ProjectManagementTaskDependencyAnalysisController).GetMethod(nameof(ProjectManagementTaskDependencyAnalysisController.AnalyzeAsync)));
        Assert.NotNull(typeof(ProjectManagementTaskDependencyAnalysisController).GetMethod(nameof(ProjectManagementTaskDependencyAnalysisController.PreviewImpactAsync)));
        Assert.Equal(typeof(Task<IActionResult>), typeof(ProjectManagementTaskDependencyAnalysisController).GetMethod(nameof(ProjectManagementTaskDependencyAnalysisController.PreviewImpactAsync))!.ReturnType);
    }

    private static async Task<SqlSugarClient> CreateDbAsync()
    {
        var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-dependency-analysis-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator", CreatedTime = Day1 }).ExecuteCommandAsync();
        return db;
    }

    private static ProjectManagementTaskEntity NewTask(string id, string? milestoneId, DateTime start, DateTime due) => new()
    {
        Id = id, TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", MilestoneId = milestoneId, TaskCode = id, Title = id,
        StartDate = start, DueDate = due, CreatedBy = "operator", CreatedTime = Day1
    };

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, "operator"), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"), new Claim(AsterErpClaimTypes.DataScope, "SELF")
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
