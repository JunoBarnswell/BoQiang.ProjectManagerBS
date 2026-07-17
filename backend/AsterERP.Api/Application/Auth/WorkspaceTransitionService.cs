using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.Auth;
using AsterERP.Contracts.System.Menus;
using AsterERP.Contracts.System.Users;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Auth;

public sealed class WorkspaceTransitionService(
    ISqlSugarClient db,
    IWorkspaceMenuReader workspaceMenuReader,
    IAuthSessionService authSessionService,
    ApplicationDatabasePermissionReader applicationDatabasePermissionReader,
    ApplicationWorkspaceUserResolver applicationWorkspaceUserResolver,
    ApplicationDatabaseBindingResolver databaseBindingResolver,
    IApplicationDatabaseConnectionFactory connectionFactory,
    ApplicationDatabaseSchemaInitializer schemaInitializer) : IWorkspaceTransitionService
{
    private const string PlatformAppCode = "SYSTEM";
    private const string PlatformDefaultRoutePath = "/platform/applications";

    public async Task<SystemUserEntity> ResolveCurrentUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = (await db.Queryable<SystemUserEntity>()
            .Where(item => !item.IsDeleted && item.Id == userId)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new ValidationException("请先登录", ErrorCodes.AuthenticationRequired);

        if (!string.Equals(user.Status, "Enabled", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("请先登录", ErrorCodes.AuthenticationRequired);
        }

        return user;
    }

    public async Task<SystemUserEntity> ResolveCurrentUserAsync(
        string userId,
        string? tenantId,
        string? appCode,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(tenantId) &&
            !string.IsNullOrWhiteSpace(appCode) &&
            !string.Equals(appCode, PlatformAppCode, StringComparison.OrdinalIgnoreCase))
        {
            var tenantApp = await LoadTenantAppAsync(tenantId, appCode, cancellationToken);
            var applicationUser = await applicationWorkspaceUserResolver.FindByIdAsync(
                userId,
                tenantApp.ConfigJson,
                tenantApp.TenantId,
                tenantApp.AppCode,
                cancellationToken);
            if (applicationUser is not null &&
                string.Equals(applicationUser.Status, "Enabled", StringComparison.OrdinalIgnoreCase))
            {
                return applicationUser;
            }

            throw new ValidationException("请先登录", ErrorCodes.AuthenticationRequired);
        }

        return await ResolveCurrentUserAsync(userId, cancellationToken);
    }

    public async Task<WorkspaceSessionSnapshot> BuildCurrentSessionAsync(
        SystemUserEntity user,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken = default)
    {
        var workspace = await ResolveWorkspaceForSessionAsync(user, tenantId, appCode, cancellationToken);
        var availableWorkspaces = string.Equals(workspace.Application.AppCode, PlatformAppCode, StringComparison.OrdinalIgnoreCase)
            ? await GetAvailableWorkspacesAsync(user.Id, cancellationToken)
            : [BuildWorkspaceResponse(workspace)];

        return await BuildWorkspaceSessionAsync(user, workspace, availableWorkspaces, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkspaceResponse>> GetAvailableWorkspacesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var user = await ResolveCurrentUserAsync(userId, cancellationToken);
        var rows = user.IsAdmin
            ? await GetAdminWorkspaceRowsAsync(user.Id, cancellationToken)
            : await GetAssignedWorkspaceRowsAsync(user.Id, cancellationToken);

        return rows
            .GroupBy(item => new { item.TenantId, item.AppCode })
            .Select(group =>
            {
                var item = group.First();
                var isAvailable = IsWorkspaceAvailable(item, now);
                var status = ResolveWorkspaceStatus(item, now);
                var systemName = ResolveSystemName(item.TenantAppSystemName, item.TenantName, item.AppName);
                var description = ResolveWorkspaceDescription(item);
                var workspaceId = BuildWorkspaceId(item.TenantId, item.AppCode);
                var isDatabaseBound = IsDatabaseBound(item.AppCode, item.TenantAppConfigJson);

                return new WorkspaceResponse(
                    workspaceId,
                    item.TenantId,
                    item.TenantName,
                    item.AppCode,
                    item.AppName,
                    item.LogoFileId,
                    group.Any(row => row.MembershipIsDefault || row.UserRoleIsDefault),
                    workspaceId,
                    item.AppCode,
                    systemName,
                    description,
                    status,
                    isAvailable,
                    isAvailable ? null : ResolveDisabledReason(item, now),
                    ResolveWorkspaceLevel(item.AppCode),
                    ResolveDefaultRoutePath(item.TenantId, item.AppCode, item.AppAdminDefaultRoutePath, item.AppDefaultRoutePath, []),
                    isDatabaseBound,
                    user.IsAdmin && !isDatabaseBound && !string.Equals(item.AppCode, PlatformAppCode, StringComparison.OrdinalIgnoreCase));
            })
            .OrderByDescending(item => item.IsAvailable)
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.TenantName)
            .ThenBy(item => item.AppCode)
            .ToList();
    }

    private Task<List<WorkspaceQueryRow>> GetAssignedWorkspaceRowsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return db.Queryable<SystemUserAppRoleEntity, SystemUserTenantMembershipEntity, SystemTenantAppEntity, SystemTenantEntity, SystemApplicationEntity>(
                (userRole, membership, tenantApp, tenant, app) =>
                    userRole.UserId == membership.UserId &&
                    userRole.TenantId == membership.TenantId &&
                    userRole.TenantId == tenantApp.TenantId &&
                    userRole.AppCode == tenantApp.AppCode &&
                    tenantApp.TenantId == tenant.Id &&
                    tenantApp.AppCode == app.AppCode)
            .Where((userRole, membership, tenantApp, tenant, app) =>
                userRole.UserId == userId &&
                !userRole.IsDeleted &&
                !membership.IsDeleted &&
                !tenantApp.IsDeleted &&
                !tenant.IsDeleted &&
                !app.IsDeleted)
            .Select((userRole, membership, tenantApp, tenant, app) => new WorkspaceQueryRow
            {
                TenantId = tenant.Id,
                TenantName = tenant.TenantName,
                TenantStatus = tenant.Status,
                TenantExpiredAt = tenant.ExpiredAt,
                AppCode = app.AppCode,
                AppName = app.AppName,
                AppStatus = app.Status,
                AppRemark = app.Remark,
                AppDefaultRoutePath = app.DefaultRoutePath,
                AppAdminDefaultRoutePath = app.AdminDefaultRoutePath,
                LogoFileId = tenantApp.LogoFileId,
                TenantAppStatus = tenantApp.Status,
                TenantAppSystemName = tenantApp.SystemName,
                TenantAppExpiredAt = tenantApp.ExpiredAt,
                TenantAppRemark = tenantApp.Remark,
                TenantAppConfigJson = tenantApp.ConfigJson,
                MembershipStatus = membership.Status,
                MembershipIsDefault = membership.IsDefault,
                UserRoleIsDefault = userRole.IsDefault
            })
            .ToListAsync(cancellationToken);
    }

    private Task<List<WorkspaceQueryRow>> GetAdminWorkspaceRowsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return db.Queryable<SystemUserTenantMembershipEntity, SystemTenantAppEntity, SystemTenantEntity, SystemApplicationEntity>(
                (membership, tenantApp, tenant, app) =>
                    membership.TenantId == tenantApp.TenantId &&
                    tenantApp.TenantId == tenant.Id &&
                    tenantApp.AppCode == app.AppCode)
            .Where((membership, tenantApp, tenant, app) =>
                membership.UserId == userId &&
                !membership.IsDeleted &&
                !tenantApp.IsDeleted &&
                !tenant.IsDeleted &&
                !app.IsDeleted)
            .Select((membership, tenantApp, tenant, app) => new WorkspaceQueryRow
            {
                TenantId = tenant.Id,
                TenantName = tenant.TenantName,
                TenantStatus = tenant.Status,
                TenantExpiredAt = tenant.ExpiredAt,
                AppCode = app.AppCode,
                AppName = app.AppName,
                AppStatus = app.Status,
                AppRemark = app.Remark,
                AppDefaultRoutePath = app.DefaultRoutePath,
                AppAdminDefaultRoutePath = app.AdminDefaultRoutePath,
                LogoFileId = tenantApp.LogoFileId,
                TenantAppStatus = tenantApp.Status,
                TenantAppSystemName = tenantApp.SystemName,
                TenantAppExpiredAt = tenantApp.ExpiredAt,
                TenantAppRemark = tenantApp.Remark,
                TenantAppConfigJson = tenantApp.ConfigJson,
                MembershipStatus = membership.Status,
                MembershipIsDefault = membership.IsDefault,
                UserRoleIsDefault = false
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ResolvedWorkspace> ResolveWorkspaceForUserAsync(
        string userId,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedTenantId = tenantId.Trim();
        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        var rows = await db.Queryable<SystemUserTenantMembershipEntity, SystemTenantAppEntity, SystemTenantEntity, SystemApplicationEntity>(
                (membership, tenantApp, tenant, app) =>
                    membership.TenantId == tenantApp.TenantId &&
                    tenantApp.TenantId == tenant.Id &&
                    tenantApp.AppCode == app.AppCode)
            .Where((membership, tenantApp, tenant, app) =>
                membership.UserId == userId &&
                membership.TenantId == normalizedTenantId &&
                tenantApp.AppCode == normalizedAppCode &&
                !membership.IsDeleted &&
                membership.Status == "Enabled" &&
                !tenantApp.IsDeleted &&
                tenantApp.Status == "Enabled" &&
                (tenantApp.ExpiredAt == null || tenantApp.ExpiredAt > DateTime.UtcNow) &&
                !tenant.IsDeleted &&
                tenant.Status == "Enabled" &&
                (tenant.ExpiredAt == null || tenant.ExpiredAt > DateTime.UtcNow) &&
                !app.IsDeleted &&
                app.Status == "Enabled")
            .Select((membership, tenantApp, tenant, app) => new
            {
                Membership = membership,
                TenantApp = tenantApp,
                Tenant = tenant,
                Application = app
            })
            .Take(1)
            .ToListAsync(cancellationToken);

        var row = rows.FirstOrDefault()
            ?? throw new ValidationException("你暂无该应用的访问权限", ErrorCodes.PermissionDenied);

        var user = await ResolveCurrentUserAsync(userId, cancellationToken);
        if (!user.IsAdmin)
        {
            var hasRole = await db.Queryable<SystemUserAppRoleEntity>()
                .Where(item =>
                    item.UserId == userId &&
                    item.TenantId == normalizedTenantId &&
                    item.AppCode == normalizedAppCode &&
                    !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (!hasRole)
            {
                throw new ValidationException("你暂无该应用的访问权限", ErrorCodes.PermissionDenied);
            }
        }

        return new ResolvedWorkspace(row.Tenant, row.Application, row.TenantApp, row.Membership);
    }

    public async Task<WorkspaceSessionSnapshot> BuildWorkspaceSessionAsync(
        SystemUserEntity user,
        ResolvedWorkspace workspace,
        IReadOnlyList<WorkspaceResponse> availableWorkspaces,
        CancellationToken cancellationToken = default)
    {
        var isPlatform = string.Equals(workspace.Application.AppCode, PlatformAppCode, StringComparison.OrdinalIgnoreCase);
        IReadOnlyList<SystemRoleEntity> roleEntities;
        if (isPlatform)
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
            await EnsureApplicationDatabaseReadyAsync(user, workspace, cancellationToken);
            roleEntities = await applicationDatabasePermissionReader.ReadRolesAsync(
                user,
                workspace.Tenant.Id,
                workspace.Application.AppCode,
                workspace.TenantApp.ConfigJson,
                cancellationToken);
        }

        var permissionCodes = isPlatform
            ? await ResolvePermissionCodesAsync(user, roleEntities, allowPlatformAdminWildcard: true, cancellationToken)
            : await applicationDatabasePermissionReader.ReadPermissionCodesAsync(
                    user,
                    workspace.Tenant.Id,
                    workspace.Application.AppCode,
                    workspace.TenantApp.ConfigJson,
                    cancellationToken);

        var menus = await workspaceMenuReader.GetVisibleTreeAsync(
            user,
            permissionCodes,
            workspace.Tenant.Id,
            workspace.Application.AppCode,
            cancellationToken);
        var defaultRoutePath = ResolveDefaultRoutePath(
            workspace.Tenant.Id,
            workspace.Application.AppCode,
            workspace.Application.AdminDefaultRoutePath,
            workspace.Application.DefaultRoutePath,
            menus);
        var dataScope = ResolveDataScope(user, roleEntities, isPlatform);
        var currentWorkspace = BuildCurrentWorkspaceResponse(workspace, defaultRoutePath);
        var branding = new BrandingResponse(
            ResolveSystemName(workspace.TenantApp.SystemName, workspace.Tenant.TenantName, workspace.Application.AppName),
            workspace.TenantApp.LogoFileId,
            workspace.TenantApp.FaviconFileId,
            workspace.TenantApp.PrimaryColor ?? "#1677ff");

        var currentUserResponse = await BuildUserResponseAsync(
            user,
            workspace,
            roleEntities.Select(item => item.Id).ToList(),
            permissionCodes,
            dataScope,
            cancellationToken);

        return new WorkspaceSessionSnapshot(
            currentWorkspace,
            currentUserResponse,
            availableWorkspaces,
            menus,
            permissionCodes,
            branding,
            defaultRoutePath);
    }

    public async Task<WorkspaceSessionSnapshot> SwitchAsync(
        SystemUserEntity user,
        string tenantId,
        string appCode,
        string? authorizationHeader,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
        {
            throw new ValidationException("租户和应用不能为空");
        }

        var workspace = await ResolveWorkspaceForUserAsync(user.Id, tenantId, appCode, cancellationToken);
        if (!string.Equals(workspace.Application.AppCode, PlatformAppCode, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureApplicationDatabaseReadyAsync(user, workspace, cancellationToken);
        }

        await authSessionService.SetCurrentWorkspaceAsync(
            authorizationHeader,
            workspace.Tenant.Id,
            workspace.Application.AppCode,
            cancellationToken);

        var availableWorkspaces = await GetAvailableWorkspacesAsync(user.Id, cancellationToken);
        return await BuildWorkspaceSessionAsync(user, workspace, availableWorkspaces, cancellationToken);
    }

    public async Task<WorkspaceSessionSnapshot> EnterApplicationBackendAsync(
        SystemUserEntity user,
        string tenantId,
        string appCode,
        string? authorizationHeader,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
        {
            throw new ValidationException("租户和应用不能为空");
        }

        if (string.Equals(appCode, PlatformAppCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("平台应用请使用平台级切换入口", ErrorCodes.PermissionDenied);
        }

        return await SwitchAsync(user, tenantId, appCode, authorizationHeader, cancellationToken);
    }

    public async Task<WorkspaceSessionSnapshot> SwitchPlatformAsync(
        SystemUserEntity user,
        string? authorizationHeader,
        CancellationToken cancellationToken = default)
    {
        var platformWorkspace = (await GetAvailableWorkspacesAsync(user.Id, cancellationToken))
            .Where(item => item.IsAvailable)
            .FirstOrDefault(item => string.Equals(item.AppCode, PlatformAppCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidationException("你暂无平台级访问权限", ErrorCodes.PermissionDenied);

        var snapshot = await SwitchAsync(
            user,
            platformWorkspace.TenantId,
            PlatformAppCode,
            authorizationHeader,
            cancellationToken);

        return snapshot with
        {
            CurrentWorkspace = snapshot.CurrentWorkspace with { DefaultRoutePath = PlatformDefaultRoutePath },
            DefaultRoutePath = PlatformDefaultRoutePath
        };
    }

    private async Task<ResolvedWorkspace> ResolveWorkspaceForSessionAsync(
        SystemUserEntity user,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken)
    {
        if (string.Equals(appCode, PlatformAppCode, StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveWorkspaceForUserAsync(user.Id, tenantId, appCode, cancellationToken);
        }

        var row = await LoadWorkspaceRowAsync(tenantId, appCode, cancellationToken);
        return new ResolvedWorkspace(
            row.Tenant,
            row.Application,
            row.TenantApp,
            new SystemUserTenantMembershipEntity
            {
                UserId = user.Id,
                TenantId = row.Tenant.Id,
                DeptId = user.DeptId,
                PositionId = user.PositionId,
                Status = user.Status,
                IsTenantAdmin = user.IsAdmin
            });
    }

    private async Task EnsureApplicationDatabaseReadyAsync(
        SystemUserEntity user,
        ResolvedWorkspace workspace,
        CancellationToken cancellationToken)
    {
        var binding = databaseBindingResolver.Resolve(
            workspace.TenantApp.ConfigJson,
            workspace.Tenant.Id,
            workspace.Application.AppCode)
            ?? throw new ValidationException("请先绑定应用数据库", ErrorCodes.ApplicationDatabaseNotBound);

        try
        {
            using var appDb = new DisposableApplicationDb(connectionFactory.Create(binding));
            await schemaInitializer.EnsureBaselineAsync(
                appDb.Client,
                workspace.Tenant.Id,
                workspace.Application.AppCode,
                user,
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

    private async Task<SystemTenantAppEntity> LoadTenantAppAsync(
        string tenantId,
        string appCode,
        CancellationToken cancellationToken)
    {
        return (await db.Queryable<SystemTenantAppEntity>()
            .Where(item =>
                item.TenantId == tenantId.Trim() &&
                item.AppCode == appCode.Trim().ToUpperInvariant() &&
                !item.IsDeleted &&
                item.Status == "Enabled")
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new ValidationException("当前应用工作区不存在或已停用", ErrorCodes.PermissionDenied);
    }

    private async Task<WorkspaceMetadataRow> LoadWorkspaceRowAsync(
        string tenantId,
        string appCode,
        CancellationToken cancellationToken)
    {
        var normalizedTenantId = tenantId.Trim();
        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        return (await db.Queryable<SystemTenantAppEntity, SystemTenantEntity, SystemApplicationEntity>(
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
            .Select((tenantApp, tenant, app) => new WorkspaceMetadataRow
            {
                TenantApp = tenantApp,
                Tenant = tenant,
                Application = app
            })
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new ValidationException("当前应用工作区不存在或已停用", ErrorCodes.PermissionDenied);
    }

    private WorkspaceResponse BuildWorkspaceResponse(ResolvedWorkspace workspace)
    {
        var workspaceId = BuildWorkspaceId(workspace.Tenant.Id, workspace.Application.AppCode);
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
            ResolveSystemName(workspace.TenantApp.SystemName, workspace.Tenant.TenantName, workspace.Application.AppName),
            workspace.TenantApp.Remark ?? workspace.Application.Remark,
            "Enabled",
            true,
            null,
            ResolveWorkspaceLevel(workspace.Application.AppCode),
            ResolveDefaultRoutePath(
                workspace.Tenant.Id,
                workspace.Application.AppCode,
                workspace.Application.AdminDefaultRoutePath,
                workspace.Application.DefaultRoutePath,
                []),
            IsDatabaseBound(workspace.Application.AppCode, workspace.TenantApp.ConfigJson),
            false);
    }

    private async Task<IReadOnlyList<string>> ResolvePermissionCodesAsync(
        SystemUserEntity user,
        IReadOnlyList<SystemRoleEntity> roles,
        bool allowPlatformAdminWildcard,
        CancellationToken cancellationToken)
    {
        if (user.IsAdmin)
        {
            var allPermissionCodes = await db.Queryable<SystemPermissionCodeEntity>()
                .Where(item => !item.IsDeleted && item.IsEnabled)
                .Select(item => item.PermissionCode)
                .ToListAsync(cancellationToken);

            if (allowPlatformAdminWildcard)
            {
                allPermissionCodes.Add("*");
            }

            return allPermissionCodes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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

    private async Task<CurrentUserResponse> BuildUserResponseAsync(
        SystemUserEntity user,
        ResolvedWorkspace workspace,
        IReadOnlyList<string> roleIds,
        IReadOnlyList<string> permissionCodes,
        string dataScope,
        CancellationToken cancellationToken)
    {
        var employments = await ResolveEmploymentsAsync(user, workspace, cancellationToken);
        var currentEmployment = ResolveCurrentEmployment(user, workspace, employments);
        var deptIds = employments
            .Select(item => item.DeptId)
            .Append(currentEmployment?.DeptId)
            .Append(workspace.Membership.DeptId)
            .Append(user.DeptId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var positionIds = employments
            .Select(item => item.PositionId)
            .Append(currentEmployment?.PositionId)
            .Append(workspace.Membership.PositionId)
            .Append(user.PositionId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CurrentUserResponse(
            user.Id,
            user.UserName,
            user.DisplayName,
            workspace.Tenant.Id,
            workspace.Tenant.TenantName,
            workspace.Application.AppCode,
            workspace.Application.AppName,
            currentEmployment?.DeptId ?? workspace.Membership.DeptId ?? user.DeptId,
            currentEmployment?.PositionId ?? workspace.Membership.PositionId ?? user.PositionId,
            roleIds,
            permissionCodes,
            dataScope,
            user.IsAdmin,
            string.Equals(workspace.Application.AppCode, PlatformAppCode, StringComparison.OrdinalIgnoreCase) && user.IsAdmin,
            workspace.Membership.IsTenantAdmin || user.IsAdmin,
            currentEmployment?.Id,
            currentEmployment?.EmploymentName,
            deptIds,
            positionIds,
            employments);
    }

    private async Task<IReadOnlyList<UserEmploymentResponse>> ResolveEmploymentsAsync(
        SystemUserEntity user,
        ResolvedWorkspace workspace,
        CancellationToken cancellationToken)
    {
        var employments = await db.Queryable<SystemUserEmploymentEntity>()
            .Where(item =>
                item.UserId == user.Id &&
                item.TenantId == workspace.Tenant.Id &&
                item.AppCode == workspace.Application.AppCode &&
                !item.IsDeleted &&
                item.Status == "Enabled")
            .OrderBy(item => item.IsPrimary, OrderByType.Desc)
            .OrderBy(item => item.SortOrder)
            .OrderBy(item => item.CreatedTime)
            .ToListAsync(cancellationToken);

        if (employments.Count == 0 &&
            !string.IsNullOrWhiteSpace(workspace.Membership.DeptId ?? user.DeptId) &&
            !string.IsNullOrWhiteSpace(workspace.Membership.PositionId ?? user.PositionId))
        {
            var deptId = workspace.Membership.DeptId ?? user.DeptId!;
            var positionId = workspace.Membership.PositionId ?? user.PositionId!;
            return
            [
                new UserEmploymentResponse(
                    string.Empty,
                    workspace.Tenant.Id,
                    workspace.Application.AppCode,
                    deptId,
                    await ResolveDepartmentNameAsync(deptId, cancellationToken),
                    positionId,
                    await ResolvePositionNameAsync(positionId, cancellationToken),
                    "当前任职",
                    true,
                    "Enabled",
                    1)
            ];
        }

        if (employments.Count == 0)
        {
            return [];
        }

        var deptIds = employments.Select(item => item.DeptId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var positionIds = employments.Select(item => item.PositionId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var departments = await db.Queryable<SystemDepartmentEntity>()
            .Where(item => deptIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var positions = await db.Queryable<SystemPositionEntity>()
            .Where(item => positionIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var deptNames = departments.ToDictionary(item => item.Id, item => item.DeptName, StringComparer.OrdinalIgnoreCase);
        var positionNames = positions.ToDictionary(item => item.Id, item => item.PositionName, StringComparer.OrdinalIgnoreCase);

        return employments
            .Select(item => new UserEmploymentResponse(
                item.Id,
                item.TenantId,
                item.AppCode,
                item.DeptId,
                deptNames.TryGetValue(item.DeptId, out var deptName) ? deptName : null,
                item.PositionId,
                positionNames.TryGetValue(item.PositionId, out var positionName) ? positionName : null,
                item.EmploymentName,
                item.IsPrimary,
                item.Status,
                item.SortOrder))
            .ToList();
    }

    private static UserEmploymentResponse? ResolveCurrentEmployment(
        SystemUserEntity user,
        ResolvedWorkspace workspace,
        IReadOnlyList<UserEmploymentResponse> employments)
    {
        return employments.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(workspace.Membership.DeptId) &&
                !string.IsNullOrWhiteSpace(workspace.Membership.PositionId) &&
                string.Equals(item.DeptId, workspace.Membership.DeptId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.PositionId, workspace.Membership.PositionId, StringComparison.OrdinalIgnoreCase))
            ?? employments.FirstOrDefault(item => item.IsPrimary)
            ?? employments.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(user.DeptId) &&
                !string.IsNullOrWhiteSpace(user.PositionId) &&
                string.Equals(item.DeptId, user.DeptId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.PositionId, user.PositionId, StringComparison.OrdinalIgnoreCase))
            ?? employments.FirstOrDefault();
    }

    private async Task<string?> ResolveDepartmentNameAsync(string deptId, CancellationToken cancellationToken) =>
        (await db.Queryable<SystemDepartmentEntity>()
            .Where(item => item.Id == deptId && !item.IsDeleted)
            .Select(item => item.DeptName)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

    private async Task<string?> ResolvePositionNameAsync(string positionId, CancellationToken cancellationToken) =>
        (await db.Queryable<SystemPositionEntity>()
            .Where(item => item.Id == positionId && !item.IsDeleted)
            .Select(item => item.PositionName)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

    private static CurrentWorkspaceResponse BuildCurrentWorkspaceResponse(ResolvedWorkspace workspace, string? defaultRoutePath)
    {
        var workspaceId = BuildWorkspaceId(workspace.Tenant.Id, workspace.Application.AppCode);
        return new CurrentWorkspaceResponse(
            workspace.Tenant.Id,
            workspace.Tenant.TenantName,
            workspace.Application.AppCode,
            workspace.Application.AppName,
            workspaceId,
            workspaceId,
            workspace.Application.AppCode,
            ResolveSystemName(workspace.TenantApp.SystemName, workspace.Tenant.TenantName, workspace.Application.AppName),
            ResolveWorkspaceLevel(workspace.Application.AppCode),
            defaultRoutePath);
    }

    private static string ResolveDefaultRoutePath(
        string tenantId,
        string appCode,
        string? adminDefaultRoutePath,
        string? legacyDefaultRoutePath,
        IReadOnlyList<MenuTreeNodeResponse> menus)
    {
        var applicationDefaultRoutePath = string.IsNullOrWhiteSpace(adminDefaultRoutePath)
            ? legacyDefaultRoutePath
            : adminDefaultRoutePath;

        if (!string.IsNullOrWhiteSpace(applicationDefaultRoutePath))
        {
            return NormalizeRouteForWorkspace(tenantId, appCode, applicationDefaultRoutePath);
        }

        var firstMenuRoute = FindFirstMenuRoute(menus);
        if (!string.IsNullOrWhiteSpace(firstMenuRoute))
        {
            return NormalizeRouteForWorkspace(tenantId, appCode, firstMenuRoute);
        }

        return NormalizeRouteForWorkspace(tenantId, appCode, "/home");
    }

    private static string? FindFirstMenuRoute(IReadOnlyList<MenuTreeNodeResponse> menus)
    {
        foreach (var menu in menus.OrderBy(item => item.SortOrder))
        {
            if (!string.IsNullOrWhiteSpace(menu.RoutePath))
            {
                return menu.RoutePath;
            }

            if (!string.IsNullOrWhiteSpace(menu.PageCode))
            {
                return $"/pages/{Uri.EscapeDataString(menu.PageCode)}";
            }

            var childRoute = FindFirstMenuRoute(menu.Children);
            if (!string.IsNullOrWhiteSpace(childRoute))
            {
                return childRoute;
            }
        }

        return null;
    }

    private static string NormalizeRouteForWorkspace(string tenantId, string appCode, string routePath)
    {
        var normalized = routePath.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "/home";
        }

        if (!normalized.StartsWith('/'))
        {
            normalized = $"/{normalized}";
        }

        if (string.Equals(appCode, PlatformAppCode, StringComparison.OrdinalIgnoreCase))
        {
            return normalized == "/home" ? PlatformDefaultRoutePath : normalized;
        }

        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        if (normalized.StartsWith("/tenants/", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        var legacyPrefix = $"/apps/{normalizedAppCode}/admin";
        if (normalized.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[legacyPrefix.Length..];
            normalized = string.IsNullOrWhiteSpace(normalized) ? "/home" : normalized;
        }

        return $"/tenants/{tenantId.Trim()}/apps/{normalizedAppCode}/admin{normalized}";
    }

    private static string BuildWorkspaceId(string tenantId, string appCode) => $"{tenantId}:{appCode}";

    private static string ResolveWorkspaceLevel(string appCode) =>
        string.Equals(appCode, PlatformAppCode, StringComparison.OrdinalIgnoreCase) ? "platform" : "application";

    private bool IsDatabaseBound(string appCode, string? tenantAppConfigJson) =>
        string.Equals(appCode, PlatformAppCode, StringComparison.OrdinalIgnoreCase) ||
        databaseBindingResolver.HasBinding(tenantAppConfigJson);

    private static string ResolveSystemName(string? systemName, string tenantName, string appName)
    {
        return string.IsNullOrWhiteSpace(systemName)
            ? $"{tenantName} {appName}"
            : systemName.Trim();
    }

    private static string? ResolveWorkspaceDescription(WorkspaceQueryRow row)
    {
        var description = string.IsNullOrWhiteSpace(row.TenantAppRemark)
            ? row.AppRemark
            : row.TenantAppRemark;

        return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    private static bool IsWorkspaceAvailable(WorkspaceQueryRow row, DateTime now) =>
        string.Equals(row.MembershipStatus, "Enabled", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(row.TenantStatus, "Enabled", StringComparison.OrdinalIgnoreCase) &&
        (row.TenantExpiredAt is null || row.TenantExpiredAt > now) &&
        string.Equals(row.TenantAppStatus, "Enabled", StringComparison.OrdinalIgnoreCase) &&
        (row.TenantAppExpiredAt is null || row.TenantAppExpiredAt > now) &&
        string.Equals(row.AppStatus, "Enabled", StringComparison.OrdinalIgnoreCase);

    private static string ResolveWorkspaceStatus(WorkspaceQueryRow row, DateTime now)
    {
        if (row.TenantExpiredAt is not null && row.TenantExpiredAt <= now)
        {
            return "TenantExpired";
        }

        if (row.TenantAppExpiredAt is not null && row.TenantAppExpiredAt <= now)
        {
            return "Expired";
        }

        if (!string.Equals(row.MembershipStatus, "Enabled", StringComparison.OrdinalIgnoreCase))
        {
            return "MembershipDisabled";
        }

        if (!string.Equals(row.TenantStatus, "Enabled", StringComparison.OrdinalIgnoreCase))
        {
            return "TenantDisabled";
        }

        if (!string.Equals(row.TenantAppStatus, "Enabled", StringComparison.OrdinalIgnoreCase))
        {
            return row.TenantAppStatus;
        }

        if (!string.Equals(row.AppStatus, "Enabled", StringComparison.OrdinalIgnoreCase))
        {
            return "ApplicationDisabled";
        }

        return "Enabled";
    }

    private static string ResolveDisabledReason(WorkspaceQueryRow row, DateTime now)
    {
        return ResolveWorkspaceStatus(row, now) switch
        {
            "MembershipDisabled" => "用户租户关系已停用",
            "TenantDisabled" => "租户已停用",
            "TenantExpired" => "租户已过期",
            "ApplicationDisabled" => "应用已停用",
            "Expired" => "系统授权已过期",
            "Enabled" => string.Empty,
            _ => "系统暂不可用"
        };
    }

    private static string ResolveDataScope(SystemUserEntity user, IReadOnlyList<SystemRoleEntity> roles, bool allowPlatformAdminScope)
    {
        if (user.IsAdmin)
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

}
