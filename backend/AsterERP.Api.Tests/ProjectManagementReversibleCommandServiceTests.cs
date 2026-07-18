using System.Reflection;
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

public sealed class ProjectManagementReversibleCommandServiceTests
{
    [Fact]
    public async Task Undo_redo_uses_handler_once_per_request_and_writes_activity_audit()
    {
        using var db = CreateDatabase();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db);
        var handler = new RecordingHandler();
        var activities = new RecordingActivityWriter();
        var service = CreateService(db, CreateUser("user-a", "tenant-a", "SYSTEM"), handler, activities);
        await RecordAsync(service, "request-1", "task-1");

        var undone = await service.UndoAsync(new ProjectManagementReversibleCommandExecuteRequest("undo-1"));
        var retry = await service.UndoAsync(new ProjectManagementReversibleCommandExecuteRequest("undo-1"));
        var redone = await service.RedoAsync(new ProjectManagementReversibleCommandExecuteRequest("redo-1"));

        Assert.Equal(ProjectManagementReversibleCommandStates.Undone, undone.State);
        Assert.Equal(undone.Id, retry.Id);
        Assert.Equal(ProjectManagementReversibleCommandStates.Applied, redone.State);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(ProjectManagementReversibleCommandDirections.Undo, handler.Requests[0].Direction);
        Assert.Equal(ProjectManagementReversibleCommandDirections.Redo, handler.Requests[1].Direction);
        Assert.Equal(2, activities.Events.Count);
        Assert.All(activities.Events, item => Assert.Equal("UndoRedo", item.Source));
    }

    [Fact]
    public async Task New_command_clears_redo_and_retention_keeps_only_latest_fifty_per_user_workspace()
    {
        using var db = CreateDatabase();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db);
        var user = CreateUser("user-a", "tenant-a", "SYSTEM");
        var service = CreateService(db, user, new RecordingHandler());
        await RecordAsync(service, "request-1", "task-1");
        await RecordAsync(service, "request-2", "task-2");
        await service.UndoAsync(new ProjectManagementReversibleCommandExecuteRequest("undo-2"));
        await RecordAsync(service, "request-3", "task-3");

        var afterNewCommand = await service.GetStackAsync();
        Assert.False(afterNewCommand.CanRedo);
        Assert.Equal(ProjectManagementReversibleCommandStates.Invalidated, (await db.Queryable<ProjectManagementReversibleCommandEntity>()
            .Where(item => item.OriginRequestId == "request-2").FirstAsync()).State);

        for (var index = 4; index <= 52; index++)
            await RecordAsync(service, $"request-{index}", $"task-{index}");

        var stack = await service.GetStackAsync();
        Assert.Equal(50, stack.Commands.Count);
        Assert.DoesNotContain(stack.Commands, item => item.AggregateId == "task-1");
        Assert.Equal(50, await db.Queryable<ProjectManagementReversibleCommandEntity>()
            .Where(item => item.TenantId == "tenant-a" && item.AppCode == "SYSTEM" && item.ActorUserId == "user-a" && item.State != ProjectManagementReversibleCommandStates.Invalidated)
            .CountAsync());
    }

    [Fact]
    public async Task Command_stack_isolated_by_user_and_workspace_and_rejects_non_system_workspace()
    {
        using var db = CreateDatabase();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db);
        var owner = CreateService(db, CreateUser("user-a", "tenant-a", "SYSTEM"), new RecordingHandler());
        await RecordAsync(owner, "request-1", "task-1");

        var otherUser = CreateService(db, CreateUser("user-b", "tenant-a", "SYSTEM"), new RecordingHandler());
        var otherTenant = CreateService(db, CreateUser("user-a", "tenant-b", "SYSTEM"), new RecordingHandler());
        Assert.Empty((await otherUser.GetStackAsync()).Commands);
        Assert.Empty((await otherTenant.GetStackAsync()).Commands);

        var mes = CreateService(db, CreateUser("user-a", "tenant-a", "MES"), new RecordingHandler());
        await Assert.ThrowsAsync<ValidationException>(() => mes.GetStackAsync());
    }

    [Fact]
    public async Task Unsupported_irreversible_commands_are_not_written_to_the_stack()
    {
        using var db = CreateDatabase();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db);
        var service = CreateService(db, CreateUser("user-a", "tenant-a", "SYSTEM"), new RecordingHandler());
        await service.TryRecordCommittedAsync(ProjectManagementReversibleCommandCapability.Instance, new ProjectManagementReversibleCommandRecordRequest(
            "purge-1", "task.purged", "project-1", "Task", "task-1", "{}", "{}", "trace-1"));

        Assert.Empty((await service.GetStackAsync()).Commands);
    }

    [Fact]
    public void Endpoint_permissions_keep_stack_read_and_replay_manage_separate()
    {
        var controller = typeof(ProjectManagementReversibleCommandsController);
        Assert.Contains(controller.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementReversibleCommandView);
        Assert.Contains(controller.GetMethod(nameof(ProjectManagementReversibleCommandsController.UndoAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementReversibleCommandManage);
        Assert.Contains(controller.GetMethod(nameof(ProjectManagementReversibleCommandsController.RedoAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementReversibleCommandManage);
    }

    private static async Task RecordAsync(ProjectManagementReversibleCommandService service, string originRequestId, string taskId) =>
        await service.TryRecordCommittedAsync(ProjectManagementReversibleCommandCapability.Instance, new ProjectManagementReversibleCommandRecordRequest(
            originRequestId,
            ProjectManagementReversibleCommandTypes.TaskUpdated,
            "project-1",
            "Task",
            taskId,
            $$"""{"taskId":"{{taskId}}","versionNo":1}""",
            $$"""{"taskId":"{{taskId}}","versionNo":2}""",
            $"trace-{originRequestId}"));

    private static ProjectManagementReversibleCommandService CreateService(SqlSugarClient db, FixedAsterErpCurrentUser user, RecordingHandler handler, RecordingActivityWriter? activityWriter = null) =>
        new(new TestWorkspaceDatabaseAccessor(db), user, new TestServiceProvider(handler), activityWriter);

    private static SqlSugarClient CreateDatabase() => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:project-management-reversible-command-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });

    private static FixedAsterErpCurrentUser CreateUser(string userId, string tenantId, string appCode) => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, userId),
        new Claim(AsterErpClaimTypes.TenantId, tenantId),
        new Claim(AsterErpClaimTypes.AppCode, appCode)
    }, "test")));

    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }

    private sealed class RecordingHandler : IProjectManagementReversibleCommandHandler
    {
        public List<ProjectManagementReversibleCommandReplayRequest> Requests { get; } = [];
        public bool CanHandle(string commandType) => ProjectManagementReversibleCommandTypes.Supported.Contains(commandType);
        public Task<ProjectManagementReversibleCommandReplayResult> ReplayAsync(ProjectManagementReversibleCommandReplayRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new ProjectManagementReversibleCommandReplayResult(request.ProjectId, request.AggregateType, request.AggregateId, 3));
        }
    }

    private sealed class TestServiceProvider(IProjectManagementReversibleCommandHandler handler) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IEnumerable<IProjectManagementReversibleCommandHandler>)
            ? new[] { handler }
            : null;
    }

    private sealed class RecordingActivityWriter : IProjectManagementActivityWriter
    {
        public List<ProjectManagementActivityEvent> Events { get; } = [];
        public Task AppendAsync(ProjectManagementActivityEvent activity, CancellationToken cancellationToken = default)
        {
            Events.Add(activity);
            return Task.CompletedTask;
        }
    }
}
