using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using SqlSugar;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataSourceSqlitePathApprovalServiceTests : IDisposable
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"astererp-sqlite-approval-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ExternalPathRequiresFourEyesApprovalAndExpiresSafely()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<
            ApplicationDataSourceEntity,
            ApplicationDataSourceSqlitePathApprovalEntity,
            ApplicationDataSourceSqlitePathApprovalAuditEntity>();
        await db.Insertable(new ApplicationDataSourceEntity
        {
            Id = "ds-approval",
            TenantId = "tenant-a",
            AppCode = "MES",
            ModuleKey = ApplicationDataCenterModuleKey.DataSource,
            ObjectCode = "sqlite_external",
            ObjectName = "SQLite 外部路径",
            ObjectType = ApplicationDataSourceType.Sqlite,
            Status = ApplicationDataCenterObjectStatus.Normal,
            ConfigJson = "{}"
        }).ExecuteCommandAsync();
        await db.Insertable(new ApplicationDataSourceEntity
        {
            Id = "ds-other",
            TenantId = "tenant-a",
            AppCode = "MES",
            ModuleKey = ApplicationDataCenterModuleKey.DataSource,
            ObjectCode = "sqlite_other",
            ObjectName = "Other SQLite source",
            ObjectType = ApplicationDataSourceType.Sqlite,
            Status = ApplicationDataCenterObjectStatus.Normal,
            ConfigJson = "{}"
        }).ExecuteCommandAsync();

        var path = Path.Combine(Path.GetTempPath(), $"astererp-approved-{Guid.NewGuid():N}.db");
        var requester = CreateCurrentUser("requester", ["app:data-center:data-source:edit"]);
        var requesterService = CreateService(db, requester);
        var pending = await requesterService.RequestAsync(new ApplicationDataSourceSqlitePathApprovalRequest(
            "ds-approval", path, "迁移窗口需要临时访问外部 SQLite 文件", DateTime.UtcNow.AddHours(1)));

        await Assert.ThrowsAsync<ValidationException>(() => requesterService.ApproveAsync(
            "ds-approval",
            new ApplicationDataSourceSqlitePathApprovalDecisionRequest(pending.Id)));

        var approver = CreateCurrentUser("approver", ["app:data-center:data-source:publish"]);
        var approverService = CreateService(db, approver);
        await Assert.ThrowsAsync<NotFoundException>(() => approverService.ApproveAsync(
            "ds-other",
            new ApplicationDataSourceSqlitePathApprovalDecisionRequest(pending.Id)));
        var approved = await approverService.ApproveAsync(
            "ds-approval",
            new ApplicationDataSourceSqlitePathApprovalDecisionRequest(pending.Id));
        Assert.Equal("Approved", approved.Status);
        await approverService.RequireActiveAsync("ds-approval", path);

        await db.Updateable<ApplicationDataSourceSqlitePathApprovalEntity>()
            .SetColumns(item => item.ExpiresAt == DateTime.UtcNow.AddMinutes(-1))
            .Where(item => item.Id == pending.Id)
            .ExecuteCommandAsync();
        await Assert.ThrowsAsync<ValidationException>(() => approverService.RequireActiveAsync("ds-approval", path));

        var auditCount = await db.Queryable<ApplicationDataSourceSqlitePathApprovalAuditEntity>().CountAsync();
        Assert.Equal(3, auditCount);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
        catch (IOException)
        {
        }
    }

    private ApplicationDataSourceSqlitePathApprovalService CreateService(ISqlSugarClient db, ICurrentUser currentUser)
    {
        var accessor = new FixedWorkspaceDatabaseAccessor(db);
        return new ApplicationDataSourceSqlitePathApprovalService(
            accessor,
            new ApplicationDataCenterWorkspaceResolver(currentUser),
            currentUser);
    }

    private SqlSugarClient CreateDb() => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source={databasePath}",
        DbType = DbType.Sqlite,
        InitKeyType = InitKeyType.Attribute,
        IsAutoCloseConnection = true
    });

    private static ICurrentUser CreateCurrentUser(string userId, IReadOnlyList<string> permissions)
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            userId,
            userId,
            "tenant-a",
            "租户 A",
            "MES",
            "租户 A MES",
            "root",
            "system-admin",
            ["role-id-admin"],
            ["admin"],
            permissions,
            "ALL",
            true,
            true,
            true,
            "平台管理员"));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return new CurrentUser(new HttpContextCurrentPrincipalAccessor(accessor));
    }

    private sealed class FixedWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }
}
