using System.Security.Cryptography;
using AsterERP.Api.Application.Auth;
using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Modules.System.Auth;
using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Api.Modules.Platform;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Security;

public sealed class AuthSessionService(
    ISqlSugarClient db,
    IConfiguration configuration,
    IDistributedCache cache,
    ApplicationDatabasePermissionReader applicationDatabasePermissionReader,
    ApplicationWorkspaceUserResolver applicationWorkspaceUserResolver,
    AuthSessionCookieWriter authSessionCookieWriter,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuthSessionService> logger) : IAuthSessionService
{
    private const int TokenByteCount = 32;
    private const string SessionCachePrefix = "auth-session:";

    public async Task<string> CreateSessionAsync(
        SystemUserEntity user,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var token = GenerateToken();
        var csrfToken = GenerateToken();
        var now = DateTime.UtcNow;
        var session = new SystemAuthSessionEntity
        {
            UserId = user.Id,
            TokenHash = HashToken(token),
            SessionVersion = 1,
            CsrfSecretHash = HashSecret(csrfToken),
            ExpiresAt = now.AddHours(ResolveSessionHours()),
            CreatedBy = user.Id,
            ClientIp = httpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext.Request.Headers.UserAgent.ToString(),
            LastSeenTime = now
        };

        await db.Insertable(session).ExecuteCommandAsync(cancellationToken);
        authSessionCookieWriter.Write(httpContext, token, csrfToken);
        return token;
    }

    public async Task<ResolvedAuthenticatedUser> ResolveAsync(string? authorizationHeader, CancellationToken cancellationToken = default)
        => await ResolveAsync(authorizationHeader, null, cancellationToken);

    public async Task<ResolvedAuthenticatedUser> ResolveAsync(
        string? authorizationHeader,
        string? sessionCookie,
        CancellationToken cancellationToken = default)
    {
        var token = ExtractStrictBearerToken(authorizationHeader) ?? NormalizeCookieToken(sessionCookie);
        if (string.IsNullOrWhiteSpace(token))
        {
            return Anonymous;
        }

        var now = DateTime.UtcNow;
        var tokenHash = HashToken(token);
        var cacheKey = BuildSessionCacheKey(tokenHash);
        var session = (await db.Queryable<SystemAuthSessionEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TokenHash == tokenHash &&
                item.RevokedAt == null &&
                item.ExpiresAt > now)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        if (session is null)
        {
            await cache.RemoveAsync(cacheKey, cancellationToken);
            return Anonymous;
        }

        var cachedBytes = await cache.GetAsync(cacheKey, cancellationToken);
        var cachedSession = cachedBytes is null
            ? null
            : JsonSerializer.Deserialize<CachedResolvedSession>(cachedBytes);
        if (cachedSession is not null &&
            cachedSession.SessionVersion == session.SessionVersion &&
            cachedSession.ExpiresAt > now)
        {
            await TouchLastSeenIfNeededAsync(cachedSession, now, cancellationToken);
            return cachedSession.User;
        }

        var workspace = await ResolveWorkspaceAsync(session.CurrentTenantId, session.CurrentAppCode, cancellationToken);
        var isPlatformWorkspace = string.Equals(workspace?.Application.AppCode, "SYSTEM", StringComparison.OrdinalIgnoreCase);
        SystemUserEntity? user;
        if (isPlatformWorkspace || workspace is null)
        {
            user = await FindPlatformUserAsync(session.UserId, cancellationToken);
        }
        else
        {
            user = await ResolveApplicationSessionUserAsync(
                session.UserId,
                workspace.Tenant.Id,
                workspace.Application.AppCode,
                workspace.TenantApp.ConfigJson,
                cacheKey,
                cancellationToken);
        }

        if (user is null || !string.Equals(user.Status, "Enabled", StringComparison.OrdinalIgnoreCase))
        {
            await cache.RemoveAsync(cacheKey, cancellationToken);
            return Anonymous;
        }

        var lastSeenSyncedAt = await TouchLastSeenIfNeededAsync(
            new CachedResolvedSession(session.Id, tokenHash, session.SessionVersion, session.ExpiresAt, session.LastSeenTime, Anonymous),
            now,
            cancellationToken)
            ? now
            : session.LastSeenTime;

        var membership = workspace is null
            ? null
            : isPlatformWorkspace
                ? (await db.Queryable<SystemUserTenantMembershipEntity>()
                .Where(item =>
                    item.UserId == user.Id &&
                    item.TenantId == workspace.Tenant.Id &&
                    !item.IsDeleted &&
                    item.Status == "Enabled")
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault()
                : new SystemUserTenantMembershipEntity
                {
                    UserId = user.Id,
                    TenantId = workspace.Tenant.Id,
                    DeptId = user.DeptId,
                    PositionId = user.PositionId,
                    Status = user.Status,
                    IsTenantAdmin = user.IsAdmin
                };

        IReadOnlyList<SystemRoleEntity> roleEntities;
        if (workspace is null)
        {
            roleEntities = [];
        }
        else if (isPlatformWorkspace)
        {
            var roleIds = await db.Queryable<SystemUserAppRoleEntity>()
                .Where(item =>
                    item.UserId == user.Id &&
                    item.TenantId == workspace.Tenant.Id &&
                    item.AppCode == workspace.Application.AppCode &&
                    !item.IsDeleted)
                .Select(item => item.RoleId)
                .ToListAsync(cancellationToken);

            roleEntities = roleIds.Count == 0
                ? []
                : await db.Queryable<SystemRoleEntity>()
                .Where(item => roleIds.Contains(item.Id) && !item.IsDeleted && item.IsEnabled)
                .ToListAsync(cancellationToken);
        }
        else
        {
            roleEntities = await applicationDatabasePermissionReader.ReadRolesAsync(
                user,
                workspace.Tenant.Id,
                workspace.Application.AppCode,
                workspace.TenantApp.ConfigJson,
                cancellationToken);
        }

        var permissionCodes = workspace is null
            ? []
            : isPlatformWorkspace
                ? await GetPermissionCodesAsync(user, roleEntities, allowPlatformAdminWildcard: true, cancellationToken)
                : (await applicationDatabasePermissionReader.ReadPermissionCodesAsync(
                        user,
                        workspace.Tenant.Id,
                        workspace.Application.AppCode,
                        workspace.TenantApp.ConfigJson,
                        cancellationToken));
        var dataScope = user.IsAdmin ? "ALL" : ResolveDataScope(user, roleEntities, isPlatformWorkspace);
        var employment = await ResolveEmploymentAsync(
            user,
            workspace?.Tenant.Id,
            workspace?.Application.AppCode,
            membership,
            cancellationToken);

        var isPlatformAdmin = (workspace is null || isPlatformWorkspace) && user.IsAdmin;
        var resolvedUser = new ResolvedAuthenticatedUser(
            user.Id,
            user.UserName,
            workspace?.Tenant.Id,
            workspace?.Tenant.TenantName,
            workspace?.Application.AppCode,
            workspace?.Application.AppName,
            employment.Current?.DeptId ?? membership?.DeptId ?? user.DeptId,
            employment.Current?.PositionId ?? membership?.PositionId ?? user.PositionId,
            roleEntities.Select(item => item.Id).ToList(),
            roleEntities.Select(item => item.RoleCode).ToList(),
            permissionCodes,
            dataScope,
            true,
            isPlatformAdmin,
            membership?.IsTenantAdmin ?? user.IsAdmin,
            user.DisplayName,
            employment.Current?.Id,
            employment.Current?.EmploymentName,
            employment.DeptIds,
            employment.PositionIds);

        await CacheResolvedSessionAsync(cacheKey, new CachedResolvedSession(
            session.Id,
            tokenHash,
            session.SessionVersion,
            session.ExpiresAt,
            lastSeenSyncedAt,
            resolvedUser));

        return resolvedUser;
    }

    public async Task SetCurrentWorkspaceAsync(
        string? authorizationHeader,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken = default)
    {
        var token = ExtractStrictBearerToken(authorizationHeader);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ValidationException("请先登录", ErrorCodes.AuthenticationRequired);
        }

        var tokenHash = HashToken(token);
        var now = DateTime.UtcNow;
        var session = (await db.Queryable<SystemAuthSessionEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TokenHash == tokenHash &&
                item.RevokedAt == null &&
                item.ExpiresAt > now)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new ValidationException("请先登录", ErrorCodes.AuthenticationRequired);

        session.CurrentTenantId = tenantId.Trim();
        session.CurrentAppCode = appCode.Trim().ToUpperInvariant();
        session.WorkspaceSwitchedAt = now;
        session.LastSeenTime = now;
        session.UpdatedTime = now;
        var previousVersion = session.SessionVersion;
        session.SessionVersion++;
        var csrfToken = GenerateToken();
        session.CsrfSecretHash = HashSecret(csrfToken);

        var updatedRows = await db.Updateable(session)
            .UpdateColumns(item => new { item.CurrentTenantId, item.CurrentAppCode, item.WorkspaceSwitchedAt, item.LastSeenTime, item.UpdatedTime, item.SessionVersion, item.CsrfSecretHash })
            .Where(item => item.Id == session.Id && item.SessionVersion == previousVersion && item.RevokedAt == null)
            .ExecuteCommandAsync(cancellationToken);
        if (updatedRows != 1)
        {
            throw new ValidationException("会话已被并发更新，请重新登录", ErrorCodes.AuthenticationRequired);
        }

        await cache.RemoveAsync(BuildSessionCacheKey(tokenHash), cancellationToken);
        RotateCsrfCookieIfCookieSession(httpContextAccessor.HttpContext, token, csrfToken);
    }

    public async Task InvalidateSessionCacheAsync(
        string? authorizationHeader,
        CancellationToken cancellationToken = default)
    {
        var token = ExtractStrictBearerToken(authorizationHeader);
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        await cache.RemoveAsync(BuildSessionCacheKey(HashToken(token)), cancellationToken);
    }

    public async Task<string> RefreshCurrentSessionAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var oldToken = ResolveRequestToken(httpContext);
        if (oldToken is null)
        {
            throw new ValidationException("请先登录", ErrorCodes.AuthenticationRequired);
        }

        var tokenHash = HashToken(oldToken);
        var session = (await db.Queryable<SystemAuthSessionEntity>()
            .Where(item => !item.IsDeleted && item.TokenHash == tokenHash && item.RevokedAt == null && item.ExpiresAt > DateTime.UtcNow)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new ValidationException("请先登录", ErrorCodes.AuthenticationRequired);

        var newToken = GenerateToken();
        var csrfToken = GenerateToken();
        var now = DateTime.UtcNow;
        var previousVersion = session.SessionVersion;
        session.TokenHash = HashToken(newToken);
        session.CsrfSecretHash = HashSecret(csrfToken);
        session.SessionVersion++;
        session.LastSeenTime = now;
        session.UpdatedTime = now;
        var updatedRows = await db.Updateable(session)
            .UpdateColumns(item => new { item.TokenHash, item.CsrfSecretHash, item.SessionVersion, item.LastSeenTime, item.UpdatedTime })
            .Where(item => item.Id == session.Id && item.TokenHash == tokenHash && item.SessionVersion == previousVersion && item.RevokedAt == null)
            .ExecuteCommandAsync(cancellationToken);
        if (updatedRows != 1)
        {
            throw new ValidationException("会话已被并发更新，请重新登录", ErrorCodes.AuthenticationRequired);
        }

        await cache.RemoveAsync(BuildSessionCacheKey(tokenHash), cancellationToken);
        authSessionCookieWriter.Write(httpContext, newToken, csrfToken);
        return newToken;
    }

    public async Task RevokeCurrentSessionAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var token = ResolveRequestToken(httpContext);
        if (token is not null)
        {
            var tokenHash = HashToken(token);
            var session = (await db.Queryable<SystemAuthSessionEntity>()
                .Where(item => !item.IsDeleted && item.TokenHash == tokenHash && item.RevokedAt == null)
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault();
            if (session is not null)
            {
                await RevokeSessionAsync(session.Id, cancellationToken);
            }
        }

        authSessionCookieWriter.Clear(httpContext);
    }

    public async Task RevokeSessionsByUserIdsAsync(IReadOnlyList<string> userIds, CancellationToken cancellationToken = default)
    {
        var normalizedUserIds = userIds
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .Select(userId => userId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedUserIds.Count == 0)
        {
            return;
        }

        var sessions = await db.Queryable<SystemAuthSessionEntity>()
            .Where(item => normalizedUserIds.Contains(item.UserId) && !item.IsDeleted && item.RevokedAt == null)
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            return;
        }

        var revokedAt = DateTime.UtcNow;
        foreach (var session in sessions)
        {
            session.RevokedAt = revokedAt;
            session.SessionVersion++;
            session.UpdatedTime = revokedAt;
            await cache.RemoveAsync(BuildSessionCacheKey(session.TokenHash), cancellationToken);
        }

        await db.Updateable(sessions)
            .UpdateColumns(item => new { item.RevokedAt, item.SessionVersion, item.UpdatedTime })
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        var session = (await db.Queryable<SystemAuthSessionEntity>()
            .Where(item => item.Id == sessionId.Trim() && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        if (session is null || session.RevokedAt.HasValue)
        {
            return;
        }

        await cache.RemoveAsync(BuildSessionCacheKey(session.TokenHash), cancellationToken);
        var revokedAt = DateTime.UtcNow;
        session.RevokedAt = revokedAt;
        session.SessionVersion++;
        session.UpdatedTime = revokedAt;

        await db.Updateable(session)
            .UpdateColumns(item => new { item.RevokedAt, item.SessionVersion, item.UpdatedTime })
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task CacheResolvedSessionAsync(string cacheKey, CachedResolvedSession session, CancellationToken cancellationToken = default)
    {
        var expiresIn = session.ExpiresAt - DateTime.UtcNow;
        if (expiresIn <= TimeSpan.Zero)
        {
            return;
        }

        await cache.SetAsync(
            cacheKey,
            JsonSerializer.SerializeToUtf8Bytes(session),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = Min(ResolveSessionCacheTtl(), expiresIn)
            },
            cancellationToken);
    }

    private async Task<bool> TouchLastSeenIfNeededAsync(
        CachedResolvedSession session,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (session.LastSeenSyncedAt.HasValue &&
            now - session.LastSeenSyncedAt.Value < ResolveLastSeenThrottle())
        {
            return false;
        }

        try
        {
            await db.Updateable<SystemAuthSessionEntity>()
                .SetColumns(item => item.LastSeenTime == now)
                .Where(item => item.Id == session.SessionId && !item.IsDeleted && item.RevokedAt == null)
                .ExecuteCommandAsync(cancellationToken);

            if (!ReferenceEquals(session.User, Anonymous))
            {
                await CacheResolvedSessionAsync(
                    BuildSessionCacheKey(session.TokenHash),
                    session with { LastSeenSyncedAt = now },
                    cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update auth session last seen time for {SessionId}", session.SessionId);
            return false;
        }
    }

    private TimeSpan ResolveSessionCacheTtl()
    {
        var seconds = configuration.GetValue("Auth:SessionCacheSeconds", 30);
        return TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 300));
    }

    private TimeSpan ResolveLastSeenThrottle()
    {
        var seconds = configuration.GetValue("Auth:LastSeenThrottleSeconds", 60);
        return TimeSpan.FromSeconds(Math.Clamp(seconds, 5, 600));
    }

    private static string BuildSessionCacheKey(string tokenHash) => $"{SessionCachePrefix}{tokenHash}";

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;

    private double ResolveSessionHours()
    {
        return double.TryParse(
            configuration["Auth:SessionHours"],
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var hours) && hours > 0
            ? hours
            : 8;
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenByteCount);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static string HashSecret(string secret) => HashToken(secret);

    public async Task<bool> ValidateCsrfTokenAsync(
        string? sessionCookie,
        string? csrfCookie,
        string? csrfHeader,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionCookie) ||
            string.IsNullOrWhiteSpace(csrfCookie) ||
            string.IsNullOrWhiteSpace(csrfHeader) ||
            !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(csrfCookie),
                System.Text.Encoding.UTF8.GetBytes(csrfHeader)))
        {
            return false;
        }

        var tokenHash = HashToken(sessionCookie.Trim());
        var session = (await db.Queryable<SystemAuthSessionEntity>()
            .Where(item => !item.IsDeleted && item.TokenHash == tokenHash && item.RevokedAt == null && item.ExpiresAt > DateTime.UtcNow)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        return session?.CsrfSecretHash is not null &&
               CryptographicOperations.FixedTimeEquals(
                   System.Text.Encoding.UTF8.GetBytes(session.CsrfSecretHash),
                   System.Text.Encoding.UTF8.GetBytes(HashSecret(csrfHeader)));
    }

    private static string? ExtractStrictBearerToken(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return null;
        }

        const string bearerPrefix = "Bearer ";
        if (!authorizationHeader.StartsWith(bearerPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var token = authorizationHeader[bearerPrefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private static string? NormalizeCookieToken(string? sessionCookie) =>
        string.IsNullOrWhiteSpace(sessionCookie) ? null : sessionCookie.Trim();

    private static string? ResolveRequestToken(HttpContext httpContext) =>
        ExtractStrictBearerToken(httpContext.Request.Headers.Authorization.ToString()) ??
        NormalizeCookieToken(httpContext.Request.Cookies[IAuthSessionService.SessionCookieName]);

    private void RotateCsrfCookieIfCookieSession(HttpContext? httpContext, string bearerToken, string csrfToken)
    {
        if (httpContext is null ||
            !string.Equals(
                NormalizeCookieToken(httpContext.Request.Cookies[IAuthSessionService.SessionCookieName]),
                bearerToken,
                StringComparison.Ordinal))
        {
            return;
        }

        authSessionCookieWriter.Write(httpContext, bearerToken, csrfToken);
    }

    private async Task<List<string>> GetPermissionCodesAsync(
        SystemUserEntity user,
        IReadOnlyList<SystemRoleEntity> roles,
        bool allowPlatformAdminWildcard,
        CancellationToken cancellationToken)
    {
        if (user.IsAdmin && allowPlatformAdminWildcard)
        {
            var allPermissionCodes = await db.Queryable<SystemPermissionCodeEntity>()
                .Where(item => !item.IsDeleted && item.IsEnabled)
                .Select(item => item.PermissionCode)
                .ToListAsync(cancellationToken);

            allPermissionCodes.Add("*");
            return allPermissionCodes
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (roles.Count == 0)
        {
            return [];
        }

        var roleIds = roles.Select(item => item.Id).ToList();
        var permissionCodeIds = await db.Queryable<SystemRolePermissionEntity>()
            .Where(item => roleIds.Contains(item.RoleId) && !item.IsDeleted)
            .Select(item => item.PermissionCodeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (permissionCodeIds.Count == 0)
        {
            return [];
        }

        return (await db.Queryable<SystemPermissionCodeEntity>()
            .Where(item => permissionCodeIds.Contains(item.Id) && !item.IsDeleted && item.IsEnabled)
            .Select(item => item.PermissionCode)
            .ToListAsync(cancellationToken))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<SystemUserEntity?> FindPlatformUserAsync(string userId, CancellationToken cancellationToken)
    {
        return (await db.Queryable<SystemUserEntity>()
            .Where(item => !item.IsDeleted && item.Id == userId)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
    }

    private async Task<EmploymentResolution> ResolveEmploymentAsync(
        SystemUserEntity user,
        string? tenantId,
        string? appCode,
        SystemUserTenantMembershipEntity? membership,
        CancellationToken cancellationToken)
    {
        var normalizedTenantId = string.IsNullOrWhiteSpace(tenantId) ? "tenant-system" : tenantId.Trim();
        var normalizedAppCode = string.IsNullOrWhiteSpace(appCode) ? "SYSTEM" : appCode.Trim().ToUpperInvariant();
        var employments = await db.Queryable<SystemUserEmploymentEntity>()
            .Where(item =>
                item.UserId == user.Id &&
                item.TenantId == normalizedTenantId &&
                item.AppCode == normalizedAppCode &&
                !item.IsDeleted &&
                item.Status == "Enabled")
            .OrderBy(item => item.IsPrimary, OrderByType.Desc)
            .OrderBy(item => item.SortOrder)
            .OrderBy(item => item.CreatedTime)
            .ToListAsync(cancellationToken);

        var current = employments.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(membership?.DeptId) &&
                !string.IsNullOrWhiteSpace(membership?.PositionId) &&
                string.Equals(item.DeptId, membership.DeptId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.PositionId, membership.PositionId, StringComparison.OrdinalIgnoreCase))
            ?? employments.FirstOrDefault(item => item.IsPrimary)
            ?? employments.FirstOrDefault();

        var deptIds = employments
            .Select(item => item.DeptId)
            .Append(current?.DeptId)
            .Append(membership?.DeptId)
            .Append(user.DeptId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var positionIds = employments
            .Select(item => item.PositionId)
            .Append(current?.PositionId)
            .Append(membership?.PositionId)
            .Append(user.PositionId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new EmploymentResolution(current, deptIds, positionIds);
    }

    private async Task<SystemUserEntity?> ResolveApplicationSessionUserAsync(
        string userId,
        string tenantId,
        string appCode,
        string? tenantAppConfigJson,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        try
        {
            return await applicationWorkspaceUserResolver.FindByIdAsync(userId, tenantAppConfigJson, tenantId, appCode, cancellationToken);
        }
        catch (ValidationException ex) when (
            ex.Code == ErrorCodes.ApplicationDatabaseNotBound ||
            ex.Code == ErrorCodes.ApplicationDatabaseConnectionFailed)
        {
            logger.LogWarning(ex, "Application session cannot resolve user {UserId}", userId);
            await cache.RemoveAsync(cacheKey, cancellationToken);
            return null;
        }
    }

    private async Task<ResolvedWorkspace?> ResolveWorkspaceAsync(
        string? tenantId,
        string? appCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
        {
            return null;
        }

        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        var tenantApp = (await db.Queryable<SystemTenantAppEntity>()
            .Where(item =>
                item.TenantId == tenantId &&
                item.AppCode == normalizedAppCode &&
                !item.IsDeleted &&
                item.Status == "Enabled" &&
                (item.ExpiredAt == null || item.ExpiredAt > DateTime.UtcNow))
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        if (tenantApp is null)
        {
            return null;
        }

        var tenant = (await db.Queryable<SystemTenantEntity>()
            .Where(item =>
                item.Id == tenantApp.TenantId &&
                !item.IsDeleted &&
                item.Status == "Enabled" &&
                (item.ExpiredAt == null || item.ExpiredAt > DateTime.UtcNow))
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        var application = (await db.Queryable<SystemApplicationEntity>()
            .Where(item => item.AppCode == tenantApp.AppCode && !item.IsDeleted && item.Status == "Enabled")
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        return tenant is null || application is null
            ? null
            : new ResolvedWorkspace(tenant, application, tenantApp);
    }

    private static string ResolveDataScope(SystemUserEntity user, IReadOnlyList<SystemRoleEntity> roles, bool allowPlatformAdminScope)
    {
        if (user.IsAdmin && allowPlatformAdminScope)
        {
            return "ALL";
        }

        if (roles.Count == 0)
        {
            return "SELF";
        }

        var rank = roles.Select(GetDataScopeRank).DefaultIfEmpty(1).Max();
        return rank switch
        {
            4 => "ALL",
            3 => "DEPT_AND_CHILD",
            2 => "DEPT",
            1 => "SELF",
            _ => "SELF"
        };
    }

    private static int GetDataScopeRank(SystemRoleEntity role)
    {
        return role.DataScope.ToUpperInvariant() switch
        {
            "ALL" => 4,
            "DEPT_AND_CHILD" => 3,
            "DEPT" => 2,
            "SELF" => 1,
            _ => 1
        };
    }

    private static readonly ResolvedAuthenticatedUser Anonymous = new(
        "anonymous",
        "anonymous",
        null,
        null,
        null,
        null,
        null,
        null,
        [],
        [],
        [],
        "SELF",
        false,
        false,
        false,
        "");

    private sealed record ResolvedWorkspace(
        SystemTenantEntity Tenant,
        SystemApplicationEntity Application,
        SystemTenantAppEntity TenantApp);

    private sealed record EmploymentResolution(
        SystemUserEmploymentEntity? Current,
        IReadOnlyList<string> DeptIds,
        IReadOnlyList<string> PositionIds);

}
