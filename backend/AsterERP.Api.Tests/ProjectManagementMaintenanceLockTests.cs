using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementMaintenanceLockTests
{
    [Fact]
    public async Task Maintenance_lock_blocks_concurrent_operation_and_releases_cleanly()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-lock-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var user = new FixedAsterErpCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AsterErpClaimTypes.UserId, "operator"),
            new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
            new Claim(AsterErpClaimTypes.AppCode, "MES")
        }, "test")));
        var service = new ProjectManagementMaintenanceLock(accessor, user);

        var operationId = await service.AcquireAsync("import", TimeSpan.FromMinutes(5));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.AcquireAsync("import", TimeSpan.FromMinutes(5)));
        await service.ReleaseAsync(operationId);
        var second = await service.AcquireAsync("import", TimeSpan.FromMinutes(5));
        Assert.NotEqual(operationId, second);
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
