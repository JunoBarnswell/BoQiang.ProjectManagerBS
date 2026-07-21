using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.Extensions.Options;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementTaskRecurrenceServiceTests
{
    [Fact]
    public async Task Generator_is_idempotent_and_series_delete_preserves_completed_history_without_child_artifacts()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var user = CreateUser();
        var taskService = new ProjectManagementTaskService(accessor, user);
        var scheduler = new RecordingScheduler();
        var recurrenceService = new ProjectManagementTaskRecurrenceService(
            accessor, user, taskService, scheduler,
            Options.Create(new ProjectManagementTaskRecurrenceOptions { DefaultGenerationWindowDays = 2, MaximumOccurrencesPerGeneration = 20 }),
            taskService);
        var source = await taskService.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("SOURCE", "源任务", Description: "保留说明"));
        var recurrence = await recurrenceService.CreateAsync("project-a", new ProjectManagementTaskRecurrenceCreateRequest(source.Id,
            new ProjectManagementTaskRecurrenceRuleRequest(ProjectManagementTaskRecurrenceFrequencies.Daily, DateTime.UtcNow, "UTC", GenerationWindowDays: 1)));
        var args = new ProjectManagementTaskRecurrenceGenerationJobArgs(recurrence.Id, "tenant-a", "SYSTEM", "operator");

        await Task.WhenAll(
            recurrenceService.GenerateAsync(args),
            recurrenceService.GenerateAsync(args),
            recurrenceService.GenerateAsync(args));

        var occurrences = await recurrenceService.QueryOccurrencesAsync(recurrence.Id);
        Assert.NotEmpty(occurrences);
        Assert.Equal(occurrences.Count, occurrences.Select(item => item.RecurrenceKey).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(occurrences.Count, await db.Queryable<ProjectManagementTaskRecurrenceOccurrenceEntity>().CountAsync(item => item.RecurrenceId == recurrence.Id && !item.IsDeleted));
        var generatedTaskIds = occurrences.Select(item => item.TaskId).ToList();
        Assert.Empty(await db.Queryable<ProjectManagementTaskCommentEntity>().Where(item => generatedTaskIds.Contains(item.TaskId) && !item.IsDeleted).ToListAsync());
        Assert.Empty(await db.Queryable<ProjectManagementTaskAttachmentEntity>().Where(item => generatedTaskIds.Contains(item.TaskId) && !item.IsDeleted).ToListAsync());
        Assert.Empty(await db.Queryable<ProjectManagementTaskReminderEntity>().Where(item => generatedTaskIds.Contains(item.TaskId) && !item.IsDeleted).ToListAsync());

        var first = occurrences[0];
        await db.Updateable<ProjectManagementTaskEntity>().SetColumns(item => new ProjectManagementTaskEntity { Status = ProjectManagementDomainRules.TaskDone }).Where(item => item.Id == first.TaskId).ExecuteCommandAsync();
        await recurrenceService.DeleteOccurrenceAsync(recurrence.Id, first.Id, new ProjectManagementTaskRecurrenceOccurrenceDeleteRequest(ProjectManagementTaskRecurrenceScopes.EntireSeries, first.VersionNo, recurrence.VersionNo));

        Assert.False((await db.Queryable<ProjectManagementTaskEntity>().InSingleAsync(first.TaskId)).IsDeleted);
        Assert.Contains(recurrence.Id, scheduler.Deleted);
    }

    private static SqlSugarClient CreateDb() => new(new ConnectionConfig { ConnectionString = $"Data Source=file:recurrence-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, "operator"), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"),
        new Claim(AsterErpClaimTypes.DataScope, "SELF"), new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.ProjectManagementProjectView)
    }, "test")));

    private sealed class RecordingScheduler : IProjectManagementTaskRecurrenceScheduler
    {
        public HashSet<string> Deleted { get; } = new(StringComparer.Ordinal);
        public Task ScheduleAsync(ProjectManagementTaskRecurrenceGenerationJobArgs args, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string recurrenceId, CancellationToken cancellationToken = default) { Deleted.Add(recurrenceId); return Task.CompletedTask; }
    }

    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }
}
