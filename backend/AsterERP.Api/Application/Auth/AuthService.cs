using System.Security.Cryptography;
using System.Text;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.Auth;
using AsterERP.Contracts.Logs;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.Auth;

public sealed class AuthService(
    ICurrentUser currentUser,
    ISqlSugarClient db,
    IWorkspaceTransitionService workspaceTransitionService,
    IAuthSessionService authSessionService,
    IPasswordHashService passwordHashService,
    ILoginLogWriter loginLogWriter,
    IConfiguration configuration) : IAuthService
{
    private const string InitialAdminPasswordRecoveryCodeConfigurationKey = "Security:InitialAdminPasswordRecovery:Code";
    private const string InitialAdminPasswordRecoveryFailureReason = "InitialAdminPasswordRecoveryFailed";
    private const string InitialAdminPasswordRecoverySuccessReason = "InitialAdminPasswordRecoverySucceeded";

    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var loginLogged = false;
        var inputUserName = request.UserName?.Trim() ?? string.Empty;
        SystemUserEntity? user = null;

        try
        {
            ValidateLoginRequest(request);

            user = (await db.Queryable<SystemUserEntity>()
                .Where(item => !item.IsDeleted && item.UserName == inputUserName)
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault();

            if (user is null)
            {
                await WriteLoginLogAsync(inputUserName, null, false, "账号不存在", httpContext, cancellationToken);
                loginLogged = true;
                throw new ValidationException("账号错误", ErrorCodes.AuthenticationRequired);
            }

            if (!string.Equals(user.Status, "Enabled", StringComparison.OrdinalIgnoreCase))
            {
                await WriteLoginLogAsync(inputUserName, user, false, "账号已停用", httpContext, cancellationToken);
                loginLogged = true;
                throw new ValidationException("账号已停用", ErrorCodes.AuthenticationRequired);
            }

            if (user.PasswordResetRequired)
            {
                await WriteLoginLogAsync(inputUserName, user, false, $"PasswordResetRequired:{user.PasswordFormatVersion}", httpContext, cancellationToken);
                loginLogged = true;
                throw new ValidationException("Password reset required.", ErrorCodes.PasswordResetRequired);
            }

            var passwordResult = passwordHashService.Verify(user.PasswordHash, request.Password);
            if (!passwordResult.Success)
            {
                if (passwordResult.RequiresPasswordReset)
                {
                    user.PasswordResetRequired = true;
                    user.PasswordFormatVersion = passwordResult.Format;
                    await db.Updateable(user).UpdateColumns(item => new { item.PasswordResetRequired, item.PasswordFormatVersion, item.UpdatedTime }).ExecuteCommandAsync(cancellationToken);
                    await WriteLoginLogAsync(inputUserName, user, false, $"PasswordResetRequired:{passwordResult.Format}", httpContext, cancellationToken);
                    loginLogged = true;
                    throw new ValidationException("Password reset required.", ErrorCodes.PasswordResetRequired);
                }
                await WriteLoginLogAsync(inputUserName, user, false, "密码错误", httpContext, cancellationToken);
                loginLogged = true;
                throw new ValidationException("密码错误", ErrorCodes.AuthenticationRequired);
            }

            if (passwordResult.NeedsRehash)
            {
                user.PasswordHash = passwordHashService.HashPassword(request.Password);
                user.PasswordResetRequired = false;
                user.PasswordFormatVersion = "v1";
                await db.Updateable(user).UpdateColumns(item => new { item.PasswordHash, item.PasswordResetRequired, item.PasswordFormatVersion, item.UpdatedTime }).ExecuteCommandAsync(cancellationToken);
            }

            var accessToken = await authSessionService.CreateSessionAsync(user, httpContext, cancellationToken);
            var availableWorkspaces = await workspaceTransitionService.GetAvailableWorkspacesAsync(user.Id, cancellationToken);
            await WriteLoginLogAsync(inputUserName, user, true, null, httpContext, cancellationToken);
            return new LoginResponse(accessToken, BuildAnonymousUserResponse(user), availableWorkspaces, null);
        }
        catch (ValidationException ex) when (!loginLogged)
        {
            await WriteLoginLogAsync(inputUserName, user, false, ex.Message, httpContext, cancellationToken);
            throw;
        }
    }

    public async Task<SessionResponse> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var currentTenantId = currentUser.GetAsterErpTenantId();
        var currentAppCode = currentUser.GetAsterErpAppCode();
        var user = await workspaceTransitionService.ResolveCurrentUserAsync(
            currentUser.GetAsterErpUserId(),
            currentTenantId,
            currentAppCode,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(currentTenantId) || string.IsNullOrWhiteSpace(currentAppCode))
        {
            var availableWorkspaces = await workspaceTransitionService.GetAvailableWorkspacesAsync(user.Id, cancellationToken);
            return new SessionResponse(
                BuildAnonymousUserResponse(user),
                availableWorkspaces,
                null,
                [],
                [],
                null);
        }

        var snapshot = await workspaceTransitionService.BuildCurrentSessionAsync(user, currentTenantId, currentAppCode, cancellationToken);
        return new SessionResponse(
            snapshot.User,
            snapshot.AvailableWorkspaces,
            snapshot.CurrentWorkspace,
            snapshot.Menus,
            snapshot.PermissionCodes,
            snapshot.Branding);
    }

    public async Task RecoverInitialAdminPasswordAsync(
        InitialAdminPasswordRecoveryRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var userName = request.UserName.Trim();
        var password = request.Password.Trim();
        var configuredRecoveryCode = configuration[InitialAdminPasswordRecoveryCodeConfigurationKey];
        var recoveryCodeMatches = !string.IsNullOrWhiteSpace(configuredRecoveryCode) &&
            FixedTimeEquals(configuredRecoveryCode, request.RecoveryCode);
        var user = (await db.Queryable<SystemUserEntity>()
            .Where(item => !item.IsDeleted && item.UserName == userName)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        if (!recoveryCodeMatches ||
            user is null ||
            !user.IsAdmin ||
            !string.Equals(user.Status, "Enabled", StringComparison.OrdinalIgnoreCase) ||
            !user.PasswordResetRequired)
        {
            await WriteLoginLogAsync(
                userName,
                user,
                false,
                InitialAdminPasswordRecoveryFailureReason,
                httpContext,
                cancellationToken);
            throw CreateInitialAdminPasswordRecoveryFailure();
        }

        var updatedAt = DateTime.UtcNow;
        var passwordHash = passwordHashService.HashPassword(password);
        var affectedRows = await db.Updateable<SystemUserEntity>()
            .SetColumns(item => new SystemUserEntity
            {
                PasswordHash = passwordHash,
                PasswordResetRequired = false,
                PasswordFormatVersion = PasswordHashPolicyOptions.CurrentVersion,
                UpdatedTime = updatedAt
            })
            .Where(item => item.Id == user.Id &&
                !item.IsDeleted &&
                item.IsAdmin &&
                item.Status == "Enabled" &&
                item.PasswordResetRequired)
            .ExecuteCommandAsync(cancellationToken);
        if (affectedRows != 1)
        {
            await WriteLoginLogAsync(
                userName,
                user,
                false,
                InitialAdminPasswordRecoveryFailureReason,
                httpContext,
                cancellationToken);
            throw CreateInitialAdminPasswordRecoveryFailure();
        }

        await authSessionService.RevokeSessionsByUserIdsAsync([user.Id], cancellationToken);
        await WriteLoginLogAsync(
            userName,
            user,
            true,
            InitialAdminPasswordRecoverySuccessReason,
            httpContext,
            cancellationToken);
    }

    public async Task<IReadOnlyList<WorkspaceResponse>> GetWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var currentTenantId = currentUser.GetAsterErpTenantId();
        var currentAppCode = currentUser.GetAsterErpAppCode();
        if (!string.IsNullOrWhiteSpace(currentTenantId) &&
            !string.IsNullOrWhiteSpace(currentAppCode) &&
            !string.Equals(currentAppCode, "SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            var user = await workspaceTransitionService.ResolveCurrentUserAsync(
                currentUser.GetAsterErpUserId(),
                currentTenantId,
                currentAppCode,
                cancellationToken);
            var snapshot = await workspaceTransitionService.BuildCurrentSessionAsync(user, currentTenantId, currentAppCode, cancellationToken);
            return snapshot.AvailableWorkspaces;
        }

        return await workspaceTransitionService.GetAvailableWorkspacesAsync(currentUser.GetAsterErpUserId(), cancellationToken);
    }

    public async Task<SwitchWorkspaceResponse> SwitchWorkspaceAsync(
        SwitchWorkspaceRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var user = await workspaceTransitionService.ResolveCurrentUserAsync(
            currentUser.GetAsterErpUserId(),
            currentUser.GetAsterErpTenantId(),
            currentUser.GetAsterErpAppCode(),
            cancellationToken);
        var snapshot = await workspaceTransitionService.SwitchAsync(
            user,
            request.TenantId,
            request.AppCode,
            httpContext.Request.Headers.Authorization.ToString(),
            cancellationToken);

        return ToSwitchWorkspaceResponse(snapshot);
    }

    public async Task<SwitchWorkspaceResponse> SwitchPlatformAsync(
        SwitchPlatformRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var user = await workspaceTransitionService.ResolveCurrentUserAsync(
            currentUser.GetAsterErpUserId(),
            currentUser.GetAsterErpTenantId(),
            currentUser.GetAsterErpAppCode(),
            cancellationToken);
        var snapshot = await workspaceTransitionService.SwitchPlatformAsync(
            user,
            httpContext.Request.Headers.Authorization.ToString(),
            cancellationToken);

        return ToSwitchWorkspaceResponse(snapshot);
    }

    public async Task<CurrentWorkspaceResponse?> GetCurrentWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var currentTenantId = currentUser.GetAsterErpTenantId();
        var currentAppCode = currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(currentTenantId) || string.IsNullOrWhiteSpace(currentAppCode))
        {
            return null;
        }

        var user = await workspaceTransitionService.ResolveCurrentUserAsync(
            currentUser.GetAsterErpUserId(),
            currentTenantId,
            currentAppCode,
            cancellationToken);
        var snapshot = await workspaceTransitionService.BuildCurrentSessionAsync(user, currentTenantId, currentAppCode, cancellationToken);
        return snapshot.CurrentWorkspace;
    }

    private static void ValidateLoginRequest(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            throw new ValidationException("用户名不能为空");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationException("密码不能为空");
        }
    }

    private static ValidationException CreateInitialAdminPasswordRecoveryFailure() =>
        new("恢复验证失败，请联系系统管理员。", ErrorCodes.AuthenticationRequired);

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private void EnsureAuthenticated()
    {
        if (!currentUser.IsAsterErpAuthenticated())
        {
            throw new ValidationException("请先登录", ErrorCodes.AuthenticationRequired);
        }
    }

    private Task WriteLoginLogAsync(
        string userName,
        SystemUserEntity? user,
        bool isSuccess,
        string? failureReason,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        return loginLogWriter.WriteAsync(
            new LoginLogWriteRequest(
                userName,
                user?.Id,
                isSuccess,
                failureReason,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString(),
                httpContext.TraceIdentifier),
            cancellationToken);
    }

    private static CurrentUserResponse BuildAnonymousUserResponse(SystemUserEntity user)
    {
        return new CurrentUserResponse(
            user.Id,
            user.UserName,
            user.DisplayName,
            null,
            null,
            null,
            null,
            user.DeptId,
            user.PositionId,
            [],
            [],
            "SELF",
            user.IsAdmin,
            user.IsAdmin,
            false);
    }

    private static SwitchWorkspaceResponse ToSwitchWorkspaceResponse(WorkspaceSessionSnapshot snapshot)
    {
        return new SwitchWorkspaceResponse(
            snapshot.CurrentWorkspace,
            snapshot.User,
            snapshot.Menus,
            snapshot.PermissionCodes,
            snapshot.Branding,
            snapshot.DefaultRoutePath);
    }
}
