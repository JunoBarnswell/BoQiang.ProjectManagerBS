using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementSavedViewServiceTests
{
    [Fact]
    public void Saved_view_state_rejects_ephemeral_or_private_shared_filters_and_normalizes_legacy_state()
    {
        Assert.Throws<AsterERP.Shared.Exceptions.ValidationException>(() => ProjectManagementSavedViewState.Normalize("{\"selectedTaskId\":\"task-a\"}", "board", false));
        Assert.Throws<AsterERP.Shared.Exceptions.ValidationException>(() => ProjectManagementSavedViewState.Normalize("{\"assigneeUserId\":\"operator\"}", "board", true));

        var normalized = ProjectManagementSavedViewState.Normalize("{\"status\":\"Todo\"}", "board", false);

        Assert.Contains("\"version\":1", normalized);
        Assert.Contains("\"viewKey\":\"board\"", normalized);
        Assert.Contains("\"status\":\"Todo\"", normalized);
    }

    [Fact]
    public async Task Saved_views_keep_structured_query_state_and_one_default_per_scope()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-saved-view-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        var service = new ProjectManagementSavedViewService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var first = await service.CreateAsync("project-a", new ProjectManagementSavedViewUpsertRequest("我的看板", "board", "{\"status\":\"Todo\"}", IsDefault: true));
        var second = await service.CreateAsync("project-a", new ProjectManagementSavedViewUpsertRequest("我的列表", "list", "{\"status\":\"InProgress\"}", IsDefault: true));
        Assert.True(second.IsDefault);
        Assert.False((await service.QueryAsync("project-a")).Single(item => item.Id == first.Id).IsDefault);
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.UpdateAsync("project-a", second.Id, new ProjectManagementSavedViewUpsertRequest("我的列表", "unknown", "{}", VersionNo: second.VersionNo)));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.UpdateAsync("project-a", second.Id, new ProjectManagementSavedViewUpsertRequest("冲突", "list", "{}", VersionNo: first.VersionNo + 1)));
    }

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, "operator"), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "MES")
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
