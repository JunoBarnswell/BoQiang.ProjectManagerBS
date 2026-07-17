using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.ApplicationConsole;
using AsterERP.Contracts.Auth;
using AsterERP.Contracts.Logs;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.Auth;

public sealed class ApplicationAuthService(
    ISqlSugarClient db,
    ICurrentUser currentUser,
    IAuthSessionService authSessionService,
    IWorkspaceTransitionService workspaceTransitionService,
    IPasswordHashService passwordHashService,
    ApplicationDatabaseBindingResolver bindingResolver,
    IApplicationDatabaseConnectionFactory connectionFactory,
    ApplicationDatabaseSchemaInitializer schemaInitializer,
    ApplicationWorkspaceUserResolver userResolver,
    ILoginLogWriter? loginLogWriter = null) : IApplicationAuthService
{
    private const string PlatformAppCode = "SYSTEM";

    public async Task<ApplicationLoginBootstrapResponse> GetBootstrapAsync(
        string tenantId,
        string appCode,
        CancellationToken cancellationToken = default)
    {
        var workspace = await LoadWorkspaceAsync(tenantId, appCode, cancellationToken);
        var binding = ResolveBindingOrNull(workspace.TenantApp.ConfigJson, workspace.Tenant.Id, workspace.Application.AppCode);
        var isBound = binding is not null;
        var isReachable = false;
        string? bindingMessage = null;

        if (binding is not null)
        {
            try
            {
                await connectionFactory.ValidateAsync(binding, cancellationToken);
                var authenticatedUser = await TryLoadAuthenticatedPlatformUserAsync(cancellationToken);
                if (authenticatedUser is not null)
                {
                    await EnsureApplicationDatabaseReadyAsync(workspace, binding, authenticatedUser, cancellationToken);
                }

                isReachable = true;
            }
            catch (ValidationException ex)
            {
                isReachable = false;
                bindingMessage = ex.Message;
            }
        }

        return new ApplicationLoginBootstrapResponse(
            workspace.Tenant.Id,
            workspace.Tenant.TenantName,
            workspace.Application.AppCode,
            workspace.Application.AppName,
            ResolveSystemName(workspace),
            workspace.TenantApp.Status,
            new ApplicationDatabaseBindingStatusResponse(
                isBound,
                isReachable,
                null,
                null,
                null,
                null,
                CanManageInitialBinding(),
                bindingMessage));
    }

    public async Task<ApplicationDatabaseBindingResponse> TestInitialDatabaseBindingAsync(
        string tenantId,
        string appCode,
        ApplicationDatabaseBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = await LoadWorkspaceAsync(tenantId, appCode, cancellationToken);
        EnsureCanManageInitialBinding();
        EnsureNotBound(workspace.TenantApp.ConfigJson);

        var options = bindingResolver.CreateOptions(request, workspace.Tenant.Id, workspace.Application.AppCode);
        await connectionFactory.ValidateAsync(options, cancellationToken);
        return BuildBindingResponse(options, "应用数据库连接成功");
    }

    public async Task<ApplicationDatabaseBindingResponse> SaveInitialDatabaseBindingAsync(
        string tenantId,
        string appCode,
        ApplicationDatabaseBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = await LoadWorkspaceAsync(tenantId, appCode, cancellationToken);
        EnsureCanManageInitialBinding();
        EnsureNotBound(workspace.TenantApp.ConfigJson);

        var options = bindingResolver.CreateOptions(request, workspace.Tenant.Id, workspace.Application.AppCode);
        await connectionFactory.ValidateAsync(options, cancellationToken);
        var platformUser = await LoadPlatformCurrentUserAsync(cancellationToken);

        using (var appDb = CreateDisposableClient(options))
        {
            await schemaInitializer.InitializeAsync(
                appDb.Client,
                workspace.Tenant.Id,
                workspace.Application.AppCode,
                platformUser,
                cancellationToken,
                workspace.TenantApp.ConfigJson);
        }

        var updatedAt = DateTime.UtcNow;
        var configJson = bindingResolver.Merge(workspace.TenantApp.ConfigJson, options, platformUser.Id, updatedAt);
        await db.Updateable<SystemTenantAppEntity>()
            .SetColumns(item => new SystemTenantAppEntity
            {
                ConfigJson = configJson,
                UpdatedBy = platformUser.Id,
                UpdatedTime = updatedAt
            })
            .Where(item => item.Id == workspace.TenantApp.Id && !item.IsDeleted)
            .ExecuteCommandAsync(cancellationToken);

        return BuildBindingResponse(options with { UpdatedAt = updatedAt, UpdatedBy = platformUser.Id }, "应用数据库绑定已保存");
    }

    public async Task<ApplicationLoginResponse> LoginAsync(
        string tenantId,
        string appCode,
        ApplicationLoginRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        ValidateLoginRequest(request);
        var workspace = await LoadWorkspaceAsync(tenantId, appCode, cancellationToken);
        var binding = bindingResolver.Resolve(workspace.TenantApp.ConfigJson, workspace.Tenant.Id, workspace.Application.AppCode)
            ?? throw new ValidationException("请先绑定应用数据库", ErrorCodes.ApplicationDatabaseNotBound);
        var platformUser = await LoadPlatformUserByUserNameAsync(request.UserName, cancellationToken);
        if (platformUser is not null)
        {
            await EnsureApplicationDatabaseReadyAsync(workspace, binding, platformUser, cancellationToken);
        }

        var user = await userResolver.FindByUserNameAsync(
            request.UserName,
            workspace.TenantApp.ConfigJson,
            workspace.Tenant.Id,
            workspace.Application.AppCode,
            cancellationToken);
        if (user is null)
        {
            throw new ValidationException("账号错误", ErrorCodes.AuthenticationRequired);
        }

        if (!string.Equals(user.Status, "Enabled", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("账号已停用", ErrorCodes.AuthenticationRequired);
        }

        if (user.PasswordResetRequired)
        {
            await WritePasswordResetAuditAsync(user, user.PasswordFormatVersion, httpContext, cancellationToken);
            throw new ValidationException("Password reset required.", ErrorCodes.PasswordResetRequired);
        }

        var passwordResult = passwordHashService.Verify(user.PasswordHash, request.Password);
        if (!passwordResult.Success)
        {
            if (passwordResult.RequiresPasswordReset)
            {
                await userResolver.MarkPasswordResetRequiredAsync(
                    user.Id,
                    passwordResult.Format,
                    workspace.TenantApp.ConfigJson,
                    workspace.Tenant.Id,
                    workspace.Application.AppCode,
                    cancellationToken);
                await WritePasswordResetAuditAsync(user, passwordResult.Format, httpContext, cancellationToken);
                throw new ValidationException("Password reset required.", ErrorCodes.PasswordResetRequired);
            }
            throw new ValidationException("密码错误", ErrorCodes.AuthenticationRequired);
        }

        if (passwordResult.NeedsRehash)
        {
        await userResolver.UpdatePasswordHashAsync(
            user.Id,
            passwordHashService.HashPassword(request.Password),
            workspace.TenantApp.ConfigJson,
            workspace.Tenant.Id,
            workspace.Application.AppCode,
            cancellationToken);
        }

        var accessToken = await authSessionService.CreateSessionAsync(user, httpContext, cancellationToken);
        await authSessionService.SetCurrentWorkspaceAsync(
            $"Bearer {accessToken}",
            workspace.Tenant.Id,
            workspace.Application.AppCode,
            cancellationToken);

        var resolvedWorkspace = BuildResolvedWorkspace(workspace, user);
        var availableWorkspaces = new[] { BuildWorkspaceResponse(workspace) };
        var snapshot = await workspaceTransitionService.BuildWorkspaceSessionAsync(
            user,
            resolvedWorkspace,
            availableWorkspaces,
            cancellationToken);

        return new ApplicationLoginResponse(
            accessToken,
            snapshot.User,
            snapshot.CurrentWorkspace,
            snapshot.Menus,
            snapshot.PermissionCodes,
            snapshot.Branding,
            snapshot.DefaultRoutePath);
    }

    private async Task<ApplicationLoginWorkspaceRow> LoadWorkspaceAsync(
        string tenantId,
        string appCode,
        CancellationToken cancellationToken)
    {
        var normalizedTenantId = NormalizeRequired(tenantId, "租户不能为空");
        var normalizedAppCode = NormalizeRequired(appCode, "应用不能为空").ToUpperInvariant();
        if (string.Equals(normalizedAppCode, PlatformAppCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("平台应用不能使用应用登录", ErrorCodes.PermissionDenied);
        }

        var workspace = (await db.Queryable<SystemTenantAppEntity, SystemTenantEntity, SystemApplicationEntity>(
                (tenantApp, tenant, app) =>
                    tenantApp.TenantId == tenant.Id &&
                    tenantApp.AppCode == app.AppCode)
            .Where((tenantApp, tenant, app) =>
                tenantApp.TenantId == normalizedTenantId &&
                tenantApp.AppCode == normalizedAppCode &&
                !tenantApp.IsDeleted &&
                tenantApp.Status == "Enabled" &&
                (tenantApp.ExpiredAt == null || tenantApp.ExpiredAt > DateTime.UtcNow) &&
                !tenant.IsDeleted &&
                tenant.Status == "Enabled" &&
                (tenant.ExpiredAt == null || tenant.ExpiredAt > DateTime.UtcNow) &&
                !app.IsDeleted &&
                app.Status == "Enabled")
            .Select((tenantApp, tenant, app) => new ApplicationLoginWorkspaceRow
            {
                TenantApp = tenantApp,
                Tenant = tenant,
                Application = app
            })
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        return workspace ?? throw new ValidationException("应用不存在或已停用", ErrorCodes.PermissionDenied);
    }

    private async Task<SystemUserEntity> LoadPlatformCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.GetAsterErpUserId();
        var user = (await db.Queryable<SystemUserEntity>()
            .Where(item => item.Id == userId && !item.IsDeleted && item.Status == "Enabled")
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        return user ?? throw new ValidationException("请先登录", ErrorCodes.AuthenticationRequired);
    }

    private async Task<SystemUserEntity?> TryLoadAuthenticatedPlatformUserAsync(CancellationToken cancellationToken)
    {
        return currentUser.IsAsterErpAuthenticated()
            ? await LoadPlatformCurrentUserAsync(cancellationToken)
            : null;
    }

    private async Task<SystemUserEntity?> LoadPlatformUserByUserNameAsync(string userName, CancellationToken cancellationToken)
    {
        var normalized = userName.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return (await db.Queryable<SystemUserEntity>()
            .Where(item =>
                item.UserName == normalized &&
                !item.IsDeleted &&
                item.Status == "Enabled")
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
    }

    private ApplicationDatabaseBindingOptions? ResolveBindingOrNull(
        string? configJson,
        string tenantId,
        string appCode)
    {
        try
        {
            return bindingResolver.Resolve(configJson, tenantId, appCode);
        }
        catch (ValidationException)
        {
            return null;
        }
    }

    private async Task EnsureApplicationDatabaseReadyAsync(
        ApplicationLoginWorkspaceRow workspace,
        ApplicationDatabaseBindingOptions binding,
        SystemUserEntity currentUserEntity,
        CancellationToken cancellationToken)
    {
        try
        {
            using var appDb = CreateDisposableClient(binding);
            await schemaInitializer.EnsureBaselineAsync(
                appDb.Client,
                workspace.Tenant.Id,
                workspace.Application.AppCode,
                currentUserEntity,
                cancellationToken,
                workspace.TenantApp.ConfigJson);
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ValidationException($"应用数据库初始化失败，请重试或检查数据库权限：{ex.Message}", ErrorCodes.ApplicationDatabaseConnectionFailed);
        }
    }

    private void EnsureCanManageInitialBinding()
    {
        if (!currentUser.IsAsterErpAuthenticated() || !currentUser.IsAsterErpPlatformAdmin())
        {
            throw new ValidationException("请先使用平台管理员登录后绑定应用数据库", ErrorCodes.PermissionDenied);
        }
    }

    private void EnsureNotBound(string? tenantAppConfigJson)
    {
        if (bindingResolver.HasBinding(tenantAppConfigJson))
        {
            throw new ValidationException("应用数据库已绑定，请使用应用管理员登录后修改", ErrorCodes.PermissionDenied);
        }
    }

    private bool CanManageInitialBinding() =>
        currentUser.IsAsterErpAuthenticated() && currentUser.IsAsterErpPlatformAdmin();

    private static void ValidateLoginRequest(ApplicationLoginRequest request)
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

    private Task WritePasswordResetAuditAsync(
        SystemUserEntity user,
        string format,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        return loginLogWriter?.WriteAsync(
                new LoginLogWriteRequest(
                    user.UserName,
                    user.Id,
                    false,
                    $"PasswordResetRequired:{format}",
                    httpContext.Connection.RemoteIpAddress?.ToString(),
                    httpContext.Request.Headers.UserAgent.ToString(),
                    httpContext.TraceIdentifier),
                cancellationToken)
            ?? Task.CompletedTask;
    }

    private static ResolvedWorkspace BuildResolvedWorkspace(
        ApplicationLoginWorkspaceRow workspace,
        SystemUserEntity user)
    {
        return new ResolvedWorkspace(
            workspace.Tenant,
            workspace.Application,
            workspace.TenantApp,
            new SystemUserTenantMembershipEntity
            {
                UserId = user.Id,
                TenantId = workspace.Tenant.Id,
                DeptId = user.DeptId,
                PositionId = user.PositionId,
                Status = user.Status,
                IsTenantAdmin = user.IsAdmin
            });
    }

    private static WorkspaceResponse BuildWorkspaceResponse(ApplicationLoginWorkspaceRow workspace)
    {
        var workspaceId = $"{workspace.Tenant.Id}:{workspace.Application.AppCode}";
        return new WorkspaceResponse(
            workspaceId,
            workspace.Tenant.Id,
            workspace.Tenant.TenantName,
            workspace.Application.AppCode,
            workspace.Application.AppName,
            workspace.TenantApp.LogoFileId,
            true,
            workspaceId,
            workspace.Application.AppCode,
            ResolveSystemName(workspace),
            workspace.TenantApp.Remark ?? workspace.Application.Remark,
            "Enabled",
            true,
            null,
            "application",
            $"/tenants/{workspace.Tenant.Id}/apps/{workspace.Application.AppCode}/admin/home",
            true,
            false);
    }

    private static string ResolveSystemName(ApplicationLoginWorkspaceRow workspace) =>
        string.IsNullOrWhiteSpace(workspace.TenantApp.SystemName)
            ? $"{workspace.Tenant.TenantName} {workspace.Application.AppName}"
            : workspace.TenantApp.SystemName.Trim();

    private static string NormalizeRequired(string value, string message)
    {
        var normalized = value.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ValidationException(message)
            : normalized;
    }

    private ApplicationDatabaseBindingResponse BuildBindingResponse(
        ApplicationDatabaseBindingOptions binding,
        string message) =>
        new(
            true,
            true,
            binding.Provider,
            binding.DisplayName,
            binding.DatabaseName,
            binding.UpdatedAt,
            CanManageInitialBinding(),
            message);

    private DisposableApplicationDb CreateDisposableClient(ApplicationDatabaseBindingOptions binding)
    {
        var client = connectionFactory.Create(binding);
        return new DisposableApplicationDb(client);
    }
}
