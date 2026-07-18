using System.Security.Claims;
using System.Linq.Expressions;
using System.Reflection;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.BackgroundJobs;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementOperationServiceTests
{
    [Fact]
    public async Task Workspace_validation_is_queued_then_worker_persists_real_phase_progress_and_events()
    {
        using var db = CreateDatabase();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var publisher = new RecordingOperationPublisher();
        var user = CreateUser("operator", "tenant-a", "MES");
        var writer = new ProjectManagementOperationWriter(new TestWorkspaceDatabaseAccessor(db), user, publisher);
        var jobs = new RecordingBackgroundJobManager();
        var service = new ProjectManagementOperationService(new TestWorkspaceDatabaseAccessor(db), user, writer, jobs);

        var operation = await service.RunWorkspaceValidationAsync();

        Assert.Equal("maintenance.workspace-validation", operation.OperationType);
        Assert.Equal("Pending", operation.Status);
        Assert.Equal("Queued", operation.Phase);
        var args = Assert.Single(jobs.Args);
        await new ProjectManagementWorkspaceValidationExecutor(new TestWorkspaceDatabaseAccessor(db), writer).ExecuteAsync(args);

        operation = await service.GetAsync(operation.Id);
        Assert.Equal("Succeeded", operation.Status);
        Assert.Equal("Completed", operation.Phase);
        Assert.Equal(100, operation.ProgressPercent);
        Assert.NotNull(operation.CompletedTime);
        Assert.Contains(publisher.Events, item => item.Phase == "ValidatingProjects" && item.ProgressPercent == 10);
        Assert.Contains(publisher.Events, item => item.Status == "Succeeded" && item.ProgressPercent == 100);
        Assert.Contains("projectCount", operation.ImpactJson, StringComparison.Ordinal);
        Assert.True(await db.Queryable<ProjectManagementOperationEventEntity>().Where(item => item.OperationId == operation.Id && item.Status == "Succeeded").AnyAsync());
    }

    [Fact]
    public async Task Cancellation_is_idempotent_and_prevents_later_success()
    {
        using var db = CreateDatabase();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var user = CreateUser("operator", "tenant-a", "MES");
        var writer = new ProjectManagementOperationWriter(new TestWorkspaceDatabaseAccessor(db), user);
        await writer.StartAsync("operation-1", "maintenance.workspace-validation", "{}", "trace-1");

        await writer.RequestCancellationAsync("operation-1");
        await writer.RequestCancellationAsync("operation-1");
        Assert.False(await writer.ReportProgressAsync("operation-1", "ValidatingTasks", 40));
        await writer.SucceedAsync("operation-1");
        await writer.CancelAsync("operation-1");

        var operation = await db.Queryable<ProjectManagementOperationEntity>().Where(item => item.Id == "operation-1").FirstAsync();
        Assert.Equal("Canceled", operation.Status);
        Assert.Equal("Canceled", operation.Phase);
        Assert.True(operation.IsCancellationRequested);
        Assert.NotNull(operation.CancellationRequestedTime);
        Assert.Equal("operator", operation.CancellationRequestedBy);
        Assert.NotNull(operation.CompletedTime);
    }

    [Fact]
    public async Task Pending_operation_cancelled_before_worker_execution_never_enters_validation()
    {
        using var db = CreateDatabase();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var user = CreateUser("operator", "tenant-a", "MES");
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var writer = new ProjectManagementOperationWriter(accessor, user);
        await writer.CreatePendingAsync("operation-pending", "maintenance.workspace-validation", "{}", "trace-pending");
        await writer.RequestCancellationAsync("operation-pending");

        await new ProjectManagementWorkspaceValidationExecutor(accessor, writer)
            .ExecuteAsync(new ProjectManagementOperationJobArgs("operation-pending", "tenant-a", "MES", "operator", "trace-pending"));

        var operation = await db.Queryable<ProjectManagementOperationEntity>().Where(item => item.Id == "operation-pending").FirstAsync();
        Assert.Equal("Canceled", operation.Status);
        Assert.Equal("Canceled", operation.Phase);
        Assert.True(operation.IsCancellationRequested);
        Assert.False(await db.Queryable<ProjectManagementOperationEventEntity>().Where(item => item.OperationId == operation.Id && item.Phase == "ValidatingProjects").AnyAsync());
    }

    [Fact]
    public async Task Background_job_manager_executes_real_operation_job_runner_with_project_management_path()
    {
        using var db = CreateDatabase();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var user = CreateUser("operator", "tenant-a", "MES");
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var writer = new ProjectManagementOperationWriter(accessor, user);
        var httpContextAccessor = new HttpContextAccessor();
        var filterRegistrar = new RecordingFilterRegistrar(httpContextAccessor);
        var runner = new ProjectManagementOperationRunner(httpContextAccessor, filterRegistrar, new ProjectManagementWorkspaceValidationExecutor(accessor, writer));
        var service = new ProjectManagementOperationService(accessor, user, writer, new ExecutingBackgroundJobManager(new ProjectManagementOperationJob(runner)));

        var operation = await service.RunWorkspaceValidationAsync();

        Assert.Equal("Succeeded", operation.Status);
        Assert.Equal("Completed", operation.Phase);
        Assert.Equal("/api/project-management/operations/maintenance/workspace-validation", Assert.Single(filterRegistrar.Paths));
    }

    [Fact]
    public async Task Real_data_permission_registrar_filters_the_platform_database_for_operation_runner()
    {
        using var mainDb = CreateDatabase();
        using var workspaceDb = CreateDatabase();
        await new ProjectManagementSchemaMigrator().MigrateAsync(mainDb, CancellationToken.None);
        await mainDb.Insertable(new[]
        {
            new ProjectManagementProjectEntity { Id = "visible", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "VISIBLE", ProjectName = "Visible", OwnerUserId = "operator" },
            new ProjectManagementProjectEntity { Id = "hidden", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "HIDDEN", ProjectName = "Hidden", OwnerUserId = "other" }
        }).ExecuteCommandAsync();
        var user = CreateUser("operator", "tenant-a", "MES");
        var accessor = new SplitWorkspaceDatabaseAccessor(mainDb, workspaceDb);
        var httpContextAccessor = new HttpContextAccessor();
        var registrar = CreateRealDataPermissionRegistrar(accessor, user, httpContextAccessor);
        var writer = new ProjectManagementOperationWriter(accessor, user);
        var runner = new ProjectManagementOperationRunner(httpContextAccessor, registrar, new ProjectManagementWorkspaceValidationExecutor(accessor, writer));
        var service = new ProjectManagementOperationService(accessor, user, writer, new ExecutingBackgroundJobManager(new ProjectManagementOperationJob(runner)));

        var operation = await service.RunWorkspaceValidationAsync();

        Assert.Contains("\"projectCount\":1", operation.ImpactJson, StringComparison.Ordinal);
        Assert.True(mainDb.DbMaintenance.IsAnyTable("pm_operations"));
        Assert.Equal("SYSTEM", (await mainDb.Queryable<ProjectManagementOperationEntity>().FirstAsync()).AppCode);
    }

    [Fact]
    public async Task Enqueue_failure_marks_pending_operation_failed()
    {
        using var db = CreateDatabase();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var user = CreateUser("operator", "tenant-a", "MES");
        var service = new ProjectManagementOperationService(new TestWorkspaceDatabaseAccessor(db), user, new ProjectManagementOperationWriter(new TestWorkspaceDatabaseAccessor(db), user), new ThrowingBackgroundJobManager());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunWorkspaceValidationAsync());
        var operation = await db.Queryable<ProjectManagementOperationEntity>().FirstAsync();
        Assert.Equal("Failed", operation.Status);
        Assert.True(await db.Queryable<ProjectManagementOperationEventEntity>().Where(item => item.OperationId == operation.Id && item.Status == "Failed").AnyAsync());
    }

    [Fact]
    public async Task Publisher_failure_does_not_rollback_persisted_operation_and_event()
    {
        using var db = CreateDatabase();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var user = CreateUser("operator", "tenant-a", "MES");
        var writer = new ProjectManagementOperationWriter(new TestWorkspaceDatabaseAccessor(db), user, new ThrowingOperationPublisher());

        await writer.CreatePendingAsync("publisher-failure", "maintenance.workspace-validation", "{}", "trace");

        Assert.True(await db.Queryable<ProjectManagementOperationEntity>().Where(item => item.Id == "publisher-failure").AnyAsync());
        Assert.True(await db.Queryable<ProjectManagementOperationEventEntity>().Where(item => item.OperationId == "publisher-failure").AnyAsync());
    }

    [Fact]
    public async Task Event_insert_failure_rolls_back_operation_state()
    {
        using var db = CreateDatabase();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        db.Ado.ExecuteCommand("DROP TABLE pm_operation_events;");
        var user = CreateUser("operator", "tenant-a", "MES");
        var writer = new ProjectManagementOperationWriter(new TestWorkspaceDatabaseAccessor(db), user);

        await Assert.ThrowsAnyAsync<Exception>(() => writer.CreatePendingAsync("event-failure", "maintenance.workspace-validation", "{}", "trace"));
        Assert.False(await db.Queryable<ProjectManagementOperationEntity>().Where(item => item.Id == "event-failure").AnyAsync());
    }

    [Fact]
    public async Task Completion_cas_loses_to_interleaved_cancellation_and_finishes_canceled()
    {
        using var db = CreateDatabase();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var user = CreateUser("operator", "tenant-a", "MES");
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var gate = new CompletionBarrierObserver();
        var completingWriter = new ProjectManagementOperationWriter(accessor, user, transitionObserver: gate);
        var cancellingWriter = new ProjectManagementOperationWriter(accessor, user);
        await completingWriter.StartAsync("cancel-race", "maintenance.workspace-validation", "{}", "trace");

        var completion = completingWriter.CompleteWithImpactAsync("cancel-race", "{\"result\":true}");
        await gate.Entered.Task;
        await cancellingWriter.RequestCancellationAsync("cancel-race");
        gate.Release.SetResult();
        await completion;

        var operation = await db.Queryable<ProjectManagementOperationEntity>().Where(item => item.Id == "cancel-race").FirstAsync();
        Assert.True(operation.IsCancellationRequested);
        Assert.Equal("Canceled", operation.Status);
        Assert.NotEqual("Succeeded", operation.Status);
    }

    [Fact]
    public async Task Failed_operation_records_error_and_owner_scope_blocks_cross_user_and_cross_tenant_access()
    {
        using var db = CreateDatabase();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var owner = CreateUser("operator", "tenant-a", "MES");
        var writer = new ProjectManagementOperationWriter(accessor, owner);
        await writer.StartAsync("operation-1", "maintenance.workspace-validation", "{}", "trace-1");
        await writer.FailAsync("operation-1", "validation failed");

        var ownerService = new ProjectManagementOperationService(accessor, owner, writer, new RecordingBackgroundJobManager());
        var operation = await ownerService.GetAsync("operation-1");
        Assert.Equal("Failed", operation.Status);
        Assert.Equal("validation failed", operation.ErrorMessage);

        var otherUserService = new ProjectManagementOperationService(accessor, CreateUser("other", "tenant-a", "MES"), new ProjectManagementOperationWriter(accessor, CreateUser("other", "tenant-a", "MES")), new RecordingBackgroundJobManager());
        await Assert.ThrowsAsync<NotFoundException>(() => otherUserService.GetAsync("operation-1"));

        var otherTenant = CreateUser("operator", "tenant-b", "MES");
        var otherTenantService = new ProjectManagementOperationService(accessor, otherTenant, new ProjectManagementOperationWriter(accessor, otherTenant), new RecordingBackgroundJobManager());
        await Assert.ThrowsAsync<NotFoundException>(() => otherTenantService.GetAsync("operation-1"));
    }

    [Fact]
    public void Operation_endpoints_keep_view_and_manage_permissions_separate()
    {
        var controller = typeof(ProjectManagementOperationsController);
        Assert.Contains(controller.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementOperationView);
        Assert.Contains(controller.GetMethod(nameof(ProjectManagementOperationsController.ValidateWorkspaceAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementOperationManage);
        Assert.Contains(controller.GetMethod(nameof(ProjectManagementOperationsController.CancelAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementOperationManage);
    }

    private static SqlSugarClient CreateDatabase() => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:project-management-operation-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
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

    private sealed class SplitWorkspaceDatabaseAccessor(ISqlSugarClient mainDb, ISqlSugarClient workspaceDb) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => mainDb;
        public ISqlSugarClient GetCurrentDb() => workspaceDb;
        public ISqlSugarClient RequireApplicationDb() => workspaceDb;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(workspaceDb);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(workspaceDb);
    }

    private static DataPermissionFilterRegistrar CreateRealDataPermissionRegistrar(
        IWorkspaceDatabaseAccessor accessor,
        FixedAsterErpCurrentUser user,
        IHttpContextAccessor httpContextAccessor)
    {
        var arguments = typeof(DataPermissionFilterRegistrar).GetConstructors().Single().GetParameters().Select(parameter =>
        {
            if (parameter.ParameterType == typeof(IWorkspaceDatabaseAccessor)) return (object)accessor;
            if (parameter.ParameterType == typeof(Volo.Abp.Users.ICurrentUser)) return user;
            if (parameter.ParameterType == typeof(IHttpContextAccessor)) return httpContextAccessor;
            if (parameter.ParameterType == typeof(DataPermissionRequestClassifier)) return new DataPermissionRequestClassifier();
            if (parameter.ParameterType.IsGenericType && parameter.ParameterType.GetGenericTypeDefinition() == typeof(IDataPermissionDescriptor<>))
                return Activator.CreateInstance(typeof(NullDescriptor<>).MakeGenericType(parameter.ParameterType.GenericTypeArguments[0]))!;
            throw new InvalidOperationException($"Unsupported registrar dependency {parameter.ParameterType.FullName}");
        }).ToArray();
        return (DataPermissionFilterRegistrar)Activator.CreateInstance(typeof(DataPermissionFilterRegistrar), arguments)!;
    }

    private sealed class NullDescriptor<TEntity> : IDataPermissionDescriptor<TEntity>
    {
        public Task<Expression<Func<TEntity, bool>>?> BuildAsync(CancellationToken cancellationToken = default) => Task.FromResult<Expression<Func<TEntity, bool>>?>(null);
    }

    private sealed class RecordingOperationPublisher : IProjectManagementOperationProgressPublisher
    {
        public List<ProjectManagementOperationProgressEvent> Events { get; } = [];

        public Task PublishAsync(string tenantId, string appCode, string userId, ProjectManagementOperationProgressEvent progressEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(progressEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingBackgroundJobManager : IBackgroundJobManager
    {
        public List<ProjectManagementOperationJobArgs> Args { get; } = [];

        public Task<string> EnqueueAsync<TArgs>(TArgs args, BackgroundJobPriority priority = BackgroundJobPriority.Normal, TimeSpan? delay = null)
        {
            if (args is ProjectManagementOperationJobArgs operationArgs) Args.Add(operationArgs);
            return Task.FromResult(Guid.NewGuid().ToString("N"));
        }
    }

    private sealed class ExecutingBackgroundJobManager(ProjectManagementOperationJob job) : IBackgroundJobManager
    {
        public async Task<string> EnqueueAsync<TArgs>(TArgs args, BackgroundJobPriority priority = BackgroundJobPriority.Normal, TimeSpan? delay = null)
        {
            if (args is not ProjectManagementOperationJobArgs operationArgs) throw new InvalidOperationException("Unexpected background job.");
            await job.ExecuteAsync(operationArgs);
            return operationArgs.OperationId;
        }
    }

    private sealed class ThrowingBackgroundJobManager : IBackgroundJobManager
    {
        public Task<string> EnqueueAsync<TArgs>(TArgs args, BackgroundJobPriority priority = BackgroundJobPriority.Normal, TimeSpan? delay = null) => throw new InvalidOperationException("queue unavailable");
    }

    private sealed class ThrowingOperationPublisher : IProjectManagementOperationProgressPublisher
    {
        public Task PublishAsync(string tenantId, string appCode, string userId, ProjectManagementOperationProgressEvent progressEvent, CancellationToken cancellationToken = default) => throw new InvalidOperationException("signalr unavailable");
    }

    private sealed class CompletionBarrierObserver : IProjectManagementOperationTransitionObserver
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task BeforePersistAsync(ProjectManagementOperationEntity operation, CancellationToken cancellationToken = default)
        {
            if (operation.Phase != "Completed") return Task.CompletedTask;
            Entered.TrySetResult();
            return Release.Task;
        }
    }

    private sealed class RecordingFilterRegistrar(IHttpContextAccessor httpContextAccessor) : IDataPermissionFilterRegistrar
    {
        public List<string> Paths { get; } = [];

        public Task<IDataPermissionFilterScope> RegisterAsync(CancellationToken cancellationToken = default)
        {
            Paths.Add(httpContextAccessor.HttpContext?.Request.Path.Value ?? string.Empty);
            return Task.FromResult<IDataPermissionFilterScope>(new NoopFilterScope());
        }
    }

    private sealed class NoopFilterScope : IDataPermissionFilterScope
    {
        public void Dispose() { }
    }
}
