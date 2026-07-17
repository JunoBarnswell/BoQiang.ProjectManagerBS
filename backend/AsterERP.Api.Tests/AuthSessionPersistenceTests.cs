using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.System.Auth;
using AsterERP.Api.Modules.System.Users;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Http;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class AuthSessionPersistenceTests
{
    [Fact]
    public async Task Created_session_persists_version_and_csrf_hash_and_validates_after_service_recreation()
    {
        using var db = CreateDatabase();
        db.CodeFirst.InitTables<SystemAuthSessionEntity>();
        var first = CreateService(db);
        var context = new DefaultHttpContext();
        var user = new SystemUserEntity { Id = "user-1", UserName = "admin", DisplayName = "Admin" };

        var token = await first.CreateSessionAsync(user, context, CancellationToken.None);
        var csrf = context.Response.Headers.SetCookie.ToString()
            .Split("astererp_csrf=", StringSplitOptions.RemoveEmptyEntries)[1]
            .Split(';')[0];
        var persisted = await db.Queryable<SystemAuthSessionEntity>().SingleAsync();

        Assert.Equal(1, persisted.SessionVersion);
        Assert.False(string.IsNullOrWhiteSpace(persisted.CsrfSecretHash));

        var restarted = CreateService(db);
        Assert.True(await restarted.ValidateCsrfTokenAsync(token, csrf, csrf, CancellationToken.None));
    }

    [Fact]
    public async Task Persisted_csrf_hash_rejects_old_secret_after_rotation()
    {
        using var db = CreateDatabase();
        db.CodeFirst.InitTables<SystemAuthSessionEntity>();
        var service = CreateService(db);
        var context = new DefaultHttpContext();
        var token = await service.CreateSessionAsync(
            new SystemUserEntity { Id = "user-1", UserName = "admin", DisplayName = "Admin" },
            context,
            CancellationToken.None);
        var oldCsrf = context.Response.Headers.SetCookie.ToString()
            .Split("astererp_csrf=", StringSplitOptions.RemoveEmptyEntries)[1]
            .Split(';')[0];

        var session = await db.Queryable<SystemAuthSessionEntity>().SingleAsync();
        session.SessionVersion++;
        session.CsrfSecretHash = "rotated-secret-hash";
        await db.Updateable(session)
            .UpdateColumns(item => new { item.SessionVersion, item.CsrfSecretHash })
            .ExecuteCommandAsync();

        Assert.False(await service.ValidateCsrfTokenAsync(token, oldCsrf, oldCsrf, CancellationToken.None));
    }

    private static AuthSessionService CreateService(ISqlSugarClient db) =>
        new(
            db,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:SessionHours"] = "8"
            }).Build(),
            CreateCache(),
            null!,
            null!,
            new AuthSessionCookieWriter(),
            new HttpContextAccessor(),
            NullLogger<AuthSessionService>.Instance);

    private static IDistributedCache CreateCache() =>
        new ServiceCollection()
            .AddDistributedMemoryCache()
            .BuildServiceProvider()
            .GetRequiredService<IDistributedCache>();

    private static SqlSugarClient CreateDatabase() =>
        new(new ConnectionConfig
        {
            DbType = DbType.Sqlite,
            ConnectionString = "DataSource=:memory:",
            IsAutoCloseConnection = false,
            InitKeyType = InitKeyType.Attribute
        });
}
