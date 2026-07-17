using System.Text.Json;
using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Api.Tests.Support;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataSourceSecurityBoundaryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"astererp-data-security-{Guid.NewGuid():N}");

    [Fact]
    public void SecretProtector_PublicSummaryNeverReturnsCipherOrPlaintext()
    {
        var protector = new ApplicationDataSecretProtector(DataProtectionProvider.Create(Path.Combine(root, "keys")));
        const string secret = "password=do-not-return;token=top-secret";

        var cipher = protector.Protect(secret);
        var summary = protector.BuildPublicSecretSummary(cipher, "secret-ref-1", DateTime.UtcNow);
        using var document = JsonDocument.Parse(summary);

        Assert.NotEqual(secret, cipher);
        Assert.DoesNotContain(secret, summary, StringComparison.Ordinal);
        Assert.DoesNotContain(cipher, summary, StringComparison.Ordinal);
        Assert.True(document.RootElement.GetProperty("hasSecret").GetBoolean());
        Assert.True(document.RootElement.GetProperty("masked").GetBoolean());
        Assert.Equal("secret-ref-1", document.RootElement.GetProperty("secretRef").GetString());
        Assert.DoesNotContain("password", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SQLiteConnectionFactory_UsesWorkspaceSandboxForRealDatabaseFile()
    {
        Directory.CreateDirectory(root);
        using var appDb = CreateAppDb();
        var currentUser = CreateCurrentUser();
        var resolver = new ApplicationDataCenterWorkspaceResolver(currentUser);
        var approvalService = new ApplicationDataSourceSqlitePathApprovalService(
            new FixedWorkspaceDatabaseAccessor(appDb), resolver, currentUser);
        var sandbox = new ApplicationDataSourceSqliteSandbox(new TestHostEnvironment(root), resolver, approvalService);
        var factory = new ApplicationDataSourceConnectionFactory(
            new ApplicationDataSecretProtector(DataProtectionProvider.Create(Path.Combine(root, "keys"))),
            new ApplicationDatabaseConnectionFactory(NullLogger<ApplicationDatabaseConnectionFactory>.Instance),
            sandbox);
        var entity = new ApplicationDataSourceEntity
        {
            Id = "security-source",
            TenantId = "tenant-a",
            AppCode = "MES",
            ModuleKey = ApplicationDataCenterModuleKey.DataSource,
            ObjectCode = "security_source",
            ObjectName = "security source",
            ObjectType = ApplicationDataSourceType.Sqlite,
            ConfigJson = ApplicationDataCenterJson.Serialize(new Dictionary<string, object?>
            {
                ["databaseName"] = "safe.db"
            })
        };

        var connection = await factory.BuildConnectionStringAsync(factory.Resolve(entity), DbType.Sqlite, entity.Id);
        var resolvedPath = new SqliteConnectionStringBuilder(connection).DataSource;
        var expectedRoot = Path.GetFullPath(Path.Combine(root, "data", "application-databases", "tenant-a", "MES"));

        Assert.StartsWith(expectedRoot + Path.DirectorySeparatorChar, Path.GetFullPath(resolvedPath), StringComparison.OrdinalIgnoreCase);
        using var database = await factory.CreateDatabaseClientAsync(entity);
        await database.Ado.ExecuteCommandAsync("CREATE TABLE security_probe (id INTEGER PRIMARY KEY, value TEXT NOT NULL)");
        Assert.True(File.Exists(resolvedPath));
        Assert.Equal(1, database.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'security_probe'"));
    }

    [Fact]
    public async Task SQLiteSandbox_RejectsTraversalAndAbsolutePathWithoutApproval()
    {
        Directory.CreateDirectory(root);
        using var appDb = CreateAppDb();
        var currentUser = CreateCurrentUser();
        var resolver = new ApplicationDataCenterWorkspaceResolver(currentUser);
        var approvalService = new ApplicationDataSourceSqlitePathApprovalService(
            new FixedWorkspaceDatabaseAccessor(appDb), resolver, currentUser);
        var sandbox = new ApplicationDataSourceSqliteSandbox(new TestHostEnvironment(root), resolver, approvalService);

        Assert.Throws<ValidationException>(() => sandbox.Resolve("../../outside.db"));
        await Assert.ThrowsAsync<ValidationException>(() => sandbox.ResolveAsync(Path.Combine(root, "outside.db"), "unapproved-source"));
        await Assert.ThrowsAsync<ValidationException>(() => sandbox.ResolveAsync("", "unapproved-source"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
        catch (IOException)
        {
        }
    }

    private SqlSugarClient CreateAppDb()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "app.db");
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });
        db.CodeFirst.InitTables<ApplicationDataSourceEntity, ApplicationDataSourceSqlitePathApprovalEntity, ApplicationDataSourceSqlitePathApprovalAuditEntity>();
        return db;
    }

    private static ICurrentUser CreateCurrentUser()
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            "security-user", "security-user", "tenant-a", "Tenant A", "MES", "Tenant A MES", "root", "system-admin",
            ["role-admin"], ["admin"], ["*"], "ALL", true, true, true, "security-user"));
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
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
