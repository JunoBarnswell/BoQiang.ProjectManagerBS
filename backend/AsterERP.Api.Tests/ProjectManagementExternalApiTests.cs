using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementExternalApiTests
{
    [Fact]
    public async Task Write_idempotency_replays_the_first_success_and_persists_caller_source_result_and_trace()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var service = new ProjectManagementExternalApiIdempotencyService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var request = new ProjectManagementExternalApiIdempotencyRequest("task.create", "external-key-001", "hash-a", "jira", "trace-a");
        var calls = 0;

        var first = await service.ExecuteAsync<TestResponse>(
            request,
            _ => Task.FromResult(new TestResponse("project-a", "task-a")),
            item => { calls++; return new ProjectManagementExternalApiResource(item.ProjectId, "Task", item.Id); });
        var replay = await service.ExecuteAsync<TestResponse>(
            request,
            _ => throw new InvalidOperationException("replay must not write twice"),
            item => new ProjectManagementExternalApiResource(item.ProjectId, "Task", item.Id));

        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(1, calls);
        Assert.Equal(first.Result, replay.Result);
        var ledger = Assert.Single(await db.Queryable<ProjectManagementExternalApiRequestEntity>().ToListAsync());
        Assert.Equal("operator", ledger.CallerUserId);
        Assert.Equal("jira", ledger.Source);
        Assert.Equal("Succeeded", ledger.Status);
        Assert.Equal("trace-a", ledger.TraceId);
        Assert.Equal("project-a", ledger.ProjectId);
    }

    [Fact]
    public async Task Write_idempotency_rejects_key_reuse_for_a_different_payload_and_records_failures()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var service = new ProjectManagementExternalApiIdempotencyService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var request = new ProjectManagementExternalApiIdempotencyRequest("task.comment.create", "external-key-002", "hash-a", "automation", "trace-b");

        await Assert.ThrowsAsync<ValidationException>(() => service.ExecuteAsync<TestResponse>(
            request,
            _ => throw new ValidationException("内容不合法", ErrorCodes.ParameterInvalid),
            item => new ProjectManagementExternalApiResource(item.ProjectId, "TaskComment", item.Id)));
        await Assert.ThrowsAsync<ProjectManagementExternalApiIdempotencyConflictException>(() => service.ExecuteAsync<TestResponse>(
            request with { RequestHash = "hash-b" },
            _ => Task.FromResult(new TestResponse("project-a", "comment-a")),
            item => new ProjectManagementExternalApiResource(item.ProjectId, "TaskComment", item.Id)));

        var ledger = Assert.Single(await db.Queryable<ProjectManagementExternalApiRequestEntity>().ToListAsync());
        Assert.Equal("Failed", ledger.Status);
        Assert.Equal(ErrorCodes.ParameterInvalid, ledger.ErrorCode);
        Assert.Equal("内容不合法", ledger.ErrorMessage);
    }

    [Fact]
    public void V1_controller_exposes_existing_permissions_idempotency_headers_and_dedicated_write_rate_limit()
    {
        var controller = typeof(ProjectManagementExternalApiController);
        var rateLimit = Assert.Single(controller.GetCustomAttributes(typeof(EnableRateLimitingAttribute), true));
        Assert.Equal(AuthenticationRateLimitPolicy.ProjectManagementExternalApiName, ((EnableRateLimitingAttribute)rateLimit).PolicyName);
        AssertPermission(controller, nameof(ProjectManagementExternalApiController.QueryProjectsAsync), PermissionCodes.ProjectManagementProjectView);
        AssertPermission(controller, nameof(ProjectManagementExternalApiController.QueryTasksAsync), PermissionCodes.ProjectManagementTaskView);
        AssertPermission(controller, nameof(ProjectManagementExternalApiController.QueryMilestonesAsync), PermissionCodes.ProjectManagementMilestoneView);
        AssertPermission(controller, nameof(ProjectManagementExternalApiController.CreateTaskAsync), PermissionCodes.ProjectManagementTaskAdd);
        AssertPermission(controller, nameof(ProjectManagementExternalApiController.UpdateTaskAsync), PermissionCodes.ProjectManagementTaskEdit);
        AssertPermission(controller, nameof(ProjectManagementExternalApiController.CreateCommentAsync), PermissionCodes.ProjectManagementCommentAdd);
        AssertPermission(controller, nameof(ProjectManagementExternalApiController.CreateAttachmentAsync), PermissionCodes.ProjectManagementAttachmentManage);
        Assert.Equal("v1", ProjectManagementExternalApiContract.ApiVersion);
        Assert.Equal("Idempotency-Key", ProjectManagementExternalApiContract.IdempotencyKeyHeader);
        Assert.Equal("If-Match", ProjectManagementExternalApiContract.VersionHeader);
    }

    [Fact]
    public void External_api_rate_limit_has_safe_defaults_and_bounded_configuration()
    {
        var defaults = AuthenticationRateLimitPolicy.ResolveProjectManagementExternalApiSettings(new ConfigurationBuilder().Build());
        Assert.Equal(60, defaults.PermitLimit);
        Assert.Equal(60, defaults.WindowSeconds);
        var bounded = AuthenticationRateLimitPolicy.ResolveProjectManagementExternalApiSettings(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ProjectManagement:ExternalApiRateLimitPermitCount"] = "1000",
                ["ProjectManagement:ExternalApiRateLimitWindowSeconds"] = "1"
            }).Build());
        Assert.Equal(600, bounded.PermitLimit);
        Assert.Equal(10, bounded.WindowSeconds);
    }

    private static void AssertPermission(Type controller, string action, string code)
    {
        var method = controller.GetMethod(action);
        Assert.NotNull(method);
        Assert.Contains(method!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == code);
    }

    private static SqlSugarClient CreateDb() => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:project-management-external-api-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity([
        new Claim(AsterErpClaimTypes.UserId, "operator"),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"),
        new Claim(AsterErpClaimTypes.DataScope, "SELF")
    ], "test")));

    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }

    private sealed record TestResponse(string ProjectId, string Id);
}
