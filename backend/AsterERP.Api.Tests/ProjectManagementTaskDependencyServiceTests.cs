using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementTaskDependencyServiceTests
{
    [Fact]
    public async Task Multi_level_finish_to_start_chain_blocks_and_unblocks_each_level()
    {
        using var db = await CreateInitializedDbAsync();
        await InsertTasksAsync(db, "project-a", "task-a", "task-b", "task-c");
        var service = CreateService(db);

        await service.CreateAsync("project-a", new ProjectManagementTaskDependencyUpsertRequest("task-a", "task-b"));
        await service.CreateAsync("project-a", new ProjectManagementTaskDependencyUpsertRequest("task-b", "task-c"));
        Assert.Equal(ProjectManagementDomainRules.TaskBlocked, await GetStatusAsync(db, "task-b"));
        Assert.Equal(ProjectManagementDomainRules.TaskBlocked, await GetStatusAsync(db, "task-c"));

        await db.Updateable<ProjectManagementTaskEntity>().SetColumns(item => item.Status == ProjectManagementDomainRules.TaskDone).Where(item => item.Id == "task-a").ExecuteCommandAsync();
        await service.RefreshBlockedStatesAsync("project-a");
        Assert.Equal(ProjectManagementDomainRules.TaskTodo, await GetStatusAsync(db, "task-b"));
        Assert.Equal(ProjectManagementDomainRules.TaskBlocked, await GetStatusAsync(db, "task-c"));

        await db.Updateable<ProjectManagementTaskEntity>().SetColumns(item => item.Status == ProjectManagementDomainRules.TaskDone).Where(item => item.Id == "task-b").ExecuteCommandAsync();
        await service.RefreshBlockedStatesAsync("project-a");
        Assert.Equal(ProjectManagementDomainRules.TaskTodo, await GetStatusAsync(db, "task-c"));
    }

    [Fact]
    public async Task Batch_import_is_atomic_and_cycle_error_contains_full_chain()
    {
        using var db = await CreateInitializedDbAsync();
        await InsertTasksAsync(db, "project-a", "task-a", "task-b", "task-c");
        var service = CreateService(db);
        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.CreateBatchAsync("project-a",
            new ProjectManagementTaskDependencyBatchCreateRequest([
                new ProjectManagementTaskDependencyUpsertRequest("task-a", "task-b"),
                new ProjectManagementTaskDependencyUpsertRequest("task-b", "task-c"),
                new ProjectManagementTaskDependencyUpsertRequest("task-c", "task-a")])));

        Assert.Contains("task-a -> task-b -> task-c -> task-a", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, await db.Queryable<ProjectManagementTaskDependencyEntity>().CountAsync());
    }

    [Fact]
    public async Task Dependencies_reject_cross_project_self_duplicate_and_concurrent_cycles()
    {
        using var db = await CreateInitializedDbAsync();
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "B", ProjectName = "B", OwnerUserId = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        await InsertTasksAsync(db, "project-a", "task-a", "task-b");
        await InsertTasksAsync(db, "project-b", "task-c");
        var service = CreateService(db);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync("project-a", new ProjectManagementTaskDependencyUpsertRequest("task-a", "task-c")));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync("project-a", new ProjectManagementTaskDependencyUpsertRequest("task-a", "task-a")));

        var results = await Task.WhenAll(
            CaptureAsync(() => service.CreateAsync("project-a", new ProjectManagementTaskDependencyUpsertRequest("task-a", "task-b"))),
            CaptureAsync(() => service.CreateAsync("project-a", new ProjectManagementTaskDependencyUpsertRequest("task-b", "task-a"))));
        Assert.Equal(1, results.Count(result => result is null));
        Assert.Single(results.OfType<ValidationException>());
        Assert.Equal(1, await db.Queryable<ProjectManagementTaskDependencyEntity>().Where(item => !item.IsDeleted).CountAsync());
    }

    [Fact]
    public async Task Soft_deleted_predecessor_keeps_exception_link_and_purge_removes_it_with_audit()
    {
        using var db = await CreateInitializedDbAsync();
        await InsertTasksAsync(db, "project-a", "task-a", "task-b");
        var audit = new RecordingActivityWriter();
        var service = CreateService(db, audit);
        await service.CreateAsync("project-a", new ProjectManagementTaskDependencyUpsertRequest("task-a", "task-b"));

        await db.Updateable<ProjectManagementTaskEntity>().SetColumns(item => item.IsDeleted == true).Where(item => item.Id == "task-a").ExecuteCommandAsync();
        await service.RefreshBlockedStatesAsync("project-a");
        var retained = await service.QueryAsync("project-a");
        var successor = await GetTaskAsync(db, "task-b");
        Assert.Single(retained);
        Assert.Contains("前置任务已删除", successor.BlockedReason, StringComparison.Ordinal);

        Assert.Equal(1, await service.PurgeForTasksAsync("project-a", ["task-a"]));
        Assert.Empty(await db.Queryable<ProjectManagementTaskDependencyEntity>().ToListAsync());
        Assert.Contains(audit.Events, item => item.ActivityType == "task.dependency.purged");
    }

    [Fact]
    public async Task Force_start_requires_owner_or_manager_reason_and_writes_audit()
    {
        using var db = await CreateInitializedDbAsync();
        await InsertTasksAsync(db, "project-a", "task-a", "task-b");
        var audit = new RecordingActivityWriter();
        var service = CreateService(db, audit);
        await service.CreateAsync("project-a", new ProjectManagementTaskDependencyUpsertRequest("task-a", "task-b"));
        var blocked = await GetTaskAsync(db, "task-b");

        var taskService = new ProjectManagementTaskService(new TestWorkspaceDatabaseAccessor(db), CreateUser("operator"), service);
        var response = await taskService.ForceStartAsync("task-b", new ProjectManagementTaskDependencyForceStartRequest("客户现场紧急处理", blocked.VersionNo));
        Assert.Equal(ProjectManagementDomainRules.TaskInProgress, response.Status);
        Assert.Equal(1, response.BlockingDependencyCount);
        Assert.StartsWith("已强制开始：", (await GetTaskAsync(db, "task-b")).BlockedReason);
        Assert.True((await taskService.GetAsync("task-b")).CanStart);
        Assert.Contains(audit.Events, item => item.ActivityType == "task.dependency.force-started");

        await Assert.ThrowsAsync<ValidationException>(() => service.ForceStartAsync("project-a", "task-b", new ProjectManagementTaskDependencyForceStartRequest(" ", response.VersionNo)));
        var unauthorized = CreateService(db, userId: "member");
        await Assert.ThrowsAsync<ValidationException>(() => unauthorized.ForceStartAsync("project-a", "task-b", new ProjectManagementTaskDependencyForceStartRequest("不应有权限", response.VersionNo)));
    }

    [Fact]
    public async Task Graph_validation_handles_large_chain_and_cancellation()
    {
        using var db = await CreateInitializedDbAsync();
        const int length = 1_000;
        var tasks = Enumerable.Range(0, length + 1).Select(index => NewTask("project-a", $"task-{index}")).ToList();
        var dependencies = Enumerable.Range(0, length).Select(index => new ProjectManagementTaskDependencyEntity
        {
            Id = Guid.NewGuid().ToString("N"), TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a",
            PredecessorTaskId = $"task-{index}", SuccessorTaskId = $"task-{index + 1}", DependencyType = "FinishToStart", CreatedBy = "operator", CreatedTime = DateTime.UtcNow
        }).ToList();
        await db.Insertable(tasks).ExecuteCommandAsync();
        await db.Insertable(dependencies).ExecuteCommandAsync();
        var service = CreateService(db);

        var cycle = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync("project-a", new ProjectManagementTaskDependencyUpsertRequest($"task-{length}", "task-0")));
        Assert.Contains("task-0", cycle.Message, StringComparison.Ordinal);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CreateAsync("project-a", new ProjectManagementTaskDependencyUpsertRequest("task-0", $"task-{length}"), cancellation.Token));
    }

    [Fact]
    public void Dependency_controller_requires_view_and_dependency_manage_permissions()
    {
        Assert.Contains(typeof(ProjectManagementTaskDependenciesController).GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskView);
        foreach (var methodName in new[] { nameof(ProjectManagementTaskDependenciesController.CreateAsync), nameof(ProjectManagementTaskDependenciesController.CreateBatchAsync), nameof(ProjectManagementTaskDependenciesController.DeleteAsync) })
            Assert.Contains(typeof(ProjectManagementTaskDependenciesController).GetMethod(methodName)!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskManageDependency);
        Assert.Contains(typeof(ProjectManagementTasksController).GetMethod(nameof(ProjectManagementTasksController.ForceStartAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskManageDependency);
    }

    private static async Task<SqlSugarClient> CreateInitializedDbAsync()
    {
        var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-dependencies-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        return db;
    }

    private static async Task InsertTasksAsync(ISqlSugarClient db, string projectId, params string[] ids) => await db.Insertable(ids.Select(id => NewTask(projectId, id)).ToList()).ExecuteCommandAsync();
    private static ProjectManagementTaskEntity NewTask(string projectId, string id) => new() { Id = id, TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = projectId, TaskCode = id, Title = id, CreatedBy = "operator", CreatedTime = DateTime.UtcNow };
    private static ProjectManagementTaskDependencyService CreateService(ISqlSugarClient db, IProjectManagementActivityWriter? audit = null, string userId = "operator") => new(new TestWorkspaceDatabaseAccessor(db), CreateUser(userId), activityWriter: audit);
    private static async Task<string> GetStatusAsync(ISqlSugarClient db, string id) => (await GetTaskAsync(db, id)).Status;
    private static async Task<ProjectManagementTaskEntity> GetTaskAsync(ISqlSugarClient db, string id) => (await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == id).Take(1).ToListAsync()).Single();
    private static async Task<Exception?> CaptureAsync(Func<Task> action) { try { await action(); return null; } catch (Exception exception) { return exception; } }
    private static FixedAsterErpCurrentUser CreateUser(string userId) => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, userId), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"), new Claim(AsterErpClaimTypes.DataScope, "SELF")
    }, "test")));

    private sealed class RecordingActivityWriter : IProjectManagementActivityWriter
    {
        public List<ProjectManagementActivityEvent> Events { get; } = [];
        public Task AppendAsync(ProjectManagementActivityEvent activity, CancellationToken cancellationToken = default) { Events.Add(activity); return Task.CompletedTask; }
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
