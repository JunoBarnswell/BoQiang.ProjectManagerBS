using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationMicroflowRuntimePermissionServiceTests : IDisposable
{
    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"astererp-runtime-microflow-permission-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task EnsureAsync_RequiresPageActionPermissionForModelFreeSqlScript()
    {
        using var db = CreateDb();
        await InsertPublishedMicroflowAsync(db);
        var currentUser = CreateCurrentUser(PermissionCodes.BuildAppRuntimePagePermission("order-page", "view"));
        var service = CreateService(db, currentUser);

        var error = await Assert.ThrowsAsync<ValidationException>(() => service.EnsureAsync(
            "sql-order-list",
            new ApplicationMicroflowExecuteRequest(PageCode: "order-page", Action: "add"),
            CancellationToken.None));

        Assert.Equal(ErrorCodes.PermissionDenied, error.Code);
    }

    [Fact]
    public async Task EnsureAsync_AllowsPageActionPermissionForModelFreeSqlScript()
    {
        using var db = CreateDb();
        await InsertPublishedMicroflowAsync(db);
        var currentUser = CreateCurrentUser(
            PermissionCodes.BuildAppRuntimePagePermission("order-page", "view"),
            PermissionCodes.BuildAppRuntimePagePermission("order-page", "add"));
        var service = CreateService(db, currentUser);

        await service.EnsureAsync(
            "sql-order-list",
            new ApplicationMicroflowExecuteRequest(PageCode: "order-page", Action: "add"),
            CancellationToken.None);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
        catch (IOException)
        {
        }
    }

    private SqlSugarClient CreateDb()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={databasePath}",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });
        db.CodeFirst.InitTables<ApplicationMicroflowEntity>();
        return db;
    }

    private static async Task InsertPublishedMicroflowAsync(ISqlSugarClient db)
    {
        await db.Insertable(new ApplicationMicroflowEntity
        {
            Id = "sql-order-list-id",
            TenantId = "tenant-a",
            AppCode = "MES",
            ModuleKey = ApplicationDataCenterModuleKey.Microflow,
            ObjectCode = "sql-order-list",
            ObjectName = "SQL订单列表",
            ObjectType = "Microflow",
            Status = ApplicationDataCenterObjectStatus.Published,
            VersionNo = 1,
            ConfigJson = ApplicationDataCenterJson.Serialize(new ApplicationMicroflowDefinition()),
            CreatedBy = "admin",
            CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();
    }

    private ApplicationMicroflowRuntimePermissionService CreateService(
        ISqlSugarClient db,
        ICurrentUser currentUser)
    {
        var pageSchemaService = new FixedRuntimePageSchemaService();
        return new ApplicationMicroflowRuntimePermissionService(
            new TestWorkspaceDatabaseAccessor(db),
            new ApplicationDataCenterWorkspaceResolver(currentUser),
            pageSchemaService,
            new RuntimeDataReadPermissionService(pageSchemaService, currentUser),
            new RuntimeDataMutationPermissionService(pageSchemaService, currentUser),
            currentUser,
            NullLogger<ApplicationMicroflowRuntimePermissionService>.Instance);
    }

    private static ICurrentUser CreateCurrentUser(params string[] permissions)
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            "admin",
            "admin",
            "tenant-a",
            "客户A",
            "MES",
            "客户A MES",
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
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
        return new CurrentUser(new HttpContextCurrentPrincipalAccessor(httpContextAccessor));
    }

    private sealed class FixedRuntimePageSchemaService : IRuntimePageSchemaService
    {
        public Task<RuntimePageSchemaResponse> GetPublishedPageAsync(
            string pageCode,
            string? previewPageId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RuntimePageSchemaResponse(
                "page-id",
                "tenant-a",
                "MES",
                pageCode,
                "订单页面",
                "designerDocument",
                null,
                PermissionCodes.BuildAppRuntimePagePermission(pageCode, "view"),
                1,
                "{\"renderer\":\"designerDocument\"}"));
        }
    }
}
