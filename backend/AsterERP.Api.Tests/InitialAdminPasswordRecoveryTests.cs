using AsterERP.Api.Application.Auth;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.Auth;
using AsterERP.Contracts.Logs;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class InitialAdminPasswordRecoveryTests
{
    [Fact]
    public async Task Recovery_with_configured_code_resets_eligible_admin_and_revokes_sessions()
    {
        using var db = CreateDatabase();
        var passwordHashService = new PasswordHashService();
        var user = CreateUser();
        await db.Insertable(user).ExecuteCommandAsync();
        var sessions = new CapturingAuthSessionService();
        var service = CreateService(db, sessions, passwordHashService, "deployment-recovery-code");

        await service.RecoverInitialAdminPasswordAsync(
            new InitialAdminPasswordRecoveryRequest("admin", "deployment-recovery-code", "new-password"),
            CreateHttpContext(),
            CancellationToken.None);

        var updated = await db.Queryable<SystemUserEntity>().SingleAsync(item => item.Id == user.Id);
        Assert.False(updated.PasswordResetRequired);
        Assert.Equal(PasswordHashPolicyOptions.CurrentVersion, updated.PasswordFormatVersion);
        Assert.True(passwordHashService.Verify(updated.PasswordHash, "new-password").Success);
        Assert.Equal([user.Id], sessions.RevokedUserIds);
    }

    [Theory]
    [InlineData(null, "deployment-recovery-code", true, true)]
    [InlineData("deployment-recovery-code", "wrong-code", true, true)]
    [InlineData("deployment-recovery-code", "deployment-recovery-code", false, true)]
    [InlineData("deployment-recovery-code", "deployment-recovery-code", true, false)]
    public async Task Recovery_rejects_missing_or_ineligible_credentials_without_changing_password(
        string? configuredCode,
        string submittedCode,
        bool isAdmin,
        bool passwordResetRequired)
    {
        using var db = CreateDatabase();
        var passwordHashService = new PasswordHashService();
        var user = CreateUser();
        user.IsAdmin = isAdmin;
        user.PasswordResetRequired = passwordResetRequired;
        await db.Insertable(user).ExecuteCommandAsync();
        var sessions = new CapturingAuthSessionService();
        var service = CreateService(db, sessions, passwordHashService, configuredCode);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.RecoverInitialAdminPasswordAsync(
            new InitialAdminPasswordRecoveryRequest("admin", submittedCode, "new-password"),
            CreateHttpContext(),
            CancellationToken.None));

        Assert.Equal("恢复验证失败，请联系系统管理员。", exception.Message);
        var unchanged = await db.Queryable<SystemUserEntity>().SingleAsync(item => item.Id == user.Id);
        Assert.Equal(user.PasswordHash, unchanged.PasswordHash);
        Assert.Equal(passwordResetRequired, unchanged.PasswordResetRequired);
        Assert.Empty(sessions.RevokedUserIds);
    }

    [Fact]
    public void Recovery_endpoint_is_anonymous_and_uses_authentication_rate_limit()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.RecoverInitialAdminPasswordAsync));

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).SingleOrDefault());
        var rateLimit = Assert.Single(method.GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true));
        Assert.Equal(AuthenticationRateLimitPolicy.Name, Assert.IsType<EnableRateLimitingAttribute>(rateLimit).PolicyName);
    }

    private static AuthService CreateService(
        SqlSugarClient db,
        IAuthSessionService authSessionService,
        IPasswordHashService passwordHashService,
        string? configuredCode)
    {
        var values = new Dictionary<string, string?>();
        if (configuredCode is not null)
        {
            values["Security:InitialAdminPasswordRecovery:Code"] = configuredCode;
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new AuthService(
            null!,
            db,
            null!,
            authSessionService,
            passwordHashService,
            new CapturingLoginLogWriter(),
            configuration);
    }

    private static SqlSugarClient CreateDatabase()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:initial-admin-password-recovery-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = false
        });
        db.CodeFirst.InitTables<SystemUserEntity>();
        return db;
    }

    private static SystemUserEntity CreateUser() => new()
    {
        Id = "admin-id",
        UserName = "admin",
        DisplayName = "Administrator",
        PasswordHash = new PasswordHashService().HashPassword("old-password"),
        PasswordFormatVersion = "legacy-pbkdf2",
        PasswordResetRequired = true,
        IsAdmin = true,
        Status = "Enabled"
    };

    private static HttpContext CreateHttpContext() => new DefaultHttpContext
    {
        TraceIdentifier = "trace-initial-admin-password-recovery"
    };

    private sealed class CapturingLoginLogWriter : ILoginLogWriter
    {
        public Task WriteAsync(LoginLogWriteRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CapturingAuthSessionService : IAuthSessionService
    {
        public List<string> RevokedUserIds { get; } = [];

        public Task<string> CreateSessionAsync(SystemUserEntity user, HttpContext httpContext, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ResolvedAuthenticatedUser> ResolveAsync(string? authorizationHeader, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ResolvedAuthenticatedUser> ResolveAsync(string? authorizationHeader, string? sessionCookie, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string> RefreshCurrentSessionAsync(HttpContext httpContext, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RevokeCurrentSessionAsync(HttpContext httpContext, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SetCurrentWorkspaceAsync(string? authorizationHeader, string tenantId, string appCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task InvalidateSessionCacheAsync(string? authorizationHeader, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RevokeSessionsByUserIdsAsync(IReadOnlyList<string> userIds, CancellationToken cancellationToken = default)
        {
            RevokedUserIds.AddRange(userIds);
            return Task.CompletedTask;
        }
    }
}
