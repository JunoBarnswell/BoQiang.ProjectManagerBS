using AsterERP.Api.Modules.System.Menus;
using AsterERP.Api.Modules.System.Parameters;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Api.Infrastructure.QueryViews;
using AsterERP.Api.Modules.Runtime;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationConsole;

public sealed class ApplicationDatabaseBaselineSeeder(
    ApplicationRbacBaselineSeeder rbacBaselineSeeder,
    ApplicationWorkflowAcceptanceBaselineSeeder workflowAcceptanceBaselineSeeder,
    ApplicationShellCapabilityResolver shellCapabilityResolver)
{
    public const string BaselineVersion = "2026.07.18.2";
    public const string BaselineParameterKey = "app.shell.baselineVersion";
    public const string CapabilitySignatureParameterKey = "app.shell.capabilitySignature";
    private const string RetiredAdminCenterMenuCode = "admin-center";
    private const string RetiredProjectManagementMenuCode = "project-management";

    public async Task<bool> IsCurrentAsync(ISqlSugarClient appDb, CancellationToken cancellationToken = default)
    {
        if (!appDb.DbMaintenance.IsAnyTable("system_parameters", false))
        {
            return false;
        }

        var value = await ReadParameterValueAsync(appDb, BaselineParameterKey, cancellationToken);

        return string.Equals(value, BaselineVersion, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> IsCurrentAsync(
        ISqlSugarClient appDb,
        string tenantId,
        string appCode,
        string? tenantAppConfigJson,
        CancellationToken cancellationToken = default)
    {
        if (!await IsCurrentAsync(appDb, cancellationToken))
        {
            return false;
        }

        var expectedSignature = BuildCapabilitySignature(tenantAppConfigJson);
        var currentSignature = await ReadParameterValueAsync(appDb, CapabilitySignatureParameterKey, cancellationToken);
        if (!string.Equals(currentSignature, expectedSignature, StringComparison.Ordinal))
        {
            return false;
        }

        return await HasRuntimeMenuDataModelAsync(appDb, tenantId, appCode, cancellationToken) &&
            !await HasRuntimeMenuRoutesToNormalizeAsync(appDb, tenantId, appCode, cancellationToken);
    }

    public async Task SeedAsync(
        ISqlSugarClient appDb,
        string tenantId,
        string appCode,
        SystemUserEntity currentUser,
        CancellationToken cancellationToken = default,
        string? tenantAppConfigJson = null)
    {
        var shellCapabilities = shellCapabilityResolver.Resolve(tenantAppConfigJson);
        var shellMenuDefinitions = ApplicationShellMenuCatalog.GetItems(shellCapabilities);
        await rbacBaselineSeeder.SeedAsync(appDb, tenantId, appCode, currentUser, cancellationToken);
        await UpsertFixedMenusAsync(appDb, tenantId, appCode, currentUser.Id, shellMenuDefinitions, cancellationToken);
        await NormalizeRuntimeMenuRoutesAsync(appDb, tenantId, appCode, currentUser.Id, cancellationToken);
        await SoftDeleteRetiredMenusAsync(appDb, tenantId, appCode, currentUser.Id, shellMenuDefinitions, cancellationToken);

        await UpsertRuntimeMenuDataModelAsync(appDb, tenantId, appCode, currentUser.Id, cancellationToken);
        await QueryViewMigrationService.MigrateDatabaseAsync(appDb, cancellationToken);
        await workflowAcceptanceBaselineSeeder.SeedAsync(appDb, tenantId, appCode, currentUser.Id, cancellationToken);
        await UpsertBaselineVersionAsync(appDb, currentUser.Id, cancellationToken);
        await UpsertCapabilitySignatureAsync(appDb, currentUser.Id, BuildCapabilitySignature(tenantAppConfigJson), cancellationToken);
    }

    private string BuildCapabilitySignature(string? tenantAppConfigJson)
    {
        var capabilities = shellCapabilityResolver.Resolve(tenantAppConfigJson)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return $"explicit:{string.Join("|", capabilities)}";
    }

    private static async Task<string?> ReadParameterValueAsync(
        ISqlSugarClient appDb,
        string parameterKey,
        CancellationToken cancellationToken)
    {
        return (await appDb.Queryable<SystemParameterEntity>()
            .Where(item => item.ParamKey == parameterKey && !item.IsDeleted && item.IsEnabled)
            .Select(item => item.ParamValue)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
    }

    private static async Task UpsertFixedMenusAsync(
        ISqlSugarClient appDb,
        string tenantId,
        string appCode,
        string currentUserId,
        IReadOnlyList<ApplicationShellMenuDefinition> shellMenuDefinitions,
        CancellationToken cancellationToken)
    {
        var menuCodes = shellMenuDefinitions.Select(item => item.MenuCode).ToArray();
        var existing = await appDb.Queryable<SystemMenuEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && menuCodes.Contains(item.MenuCode))
            .ToListAsync(cancellationToken);
        var byCode = existing.ToDictionary(item => item.MenuCode, StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        var inserts = new List<SystemMenuEntity>();
        var updates = new List<SystemMenuEntity>();
        var normalizedAppCode = appCode.Trim().ToUpperInvariant();

        foreach (var definition in shellMenuDefinitions)
        {
            if (!byCode.TryGetValue(definition.MenuCode, out var entity))
            {
                inserts.Add(new SystemMenuEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    TenantId = tenantId,
                    AppCode = normalizedAppCode,
                    MenuName = definition.MenuName,
                    MenuCode = definition.MenuCode,
                    ParentCode = definition.ParentCode,
                    RoutePath = definition.RoutePath,
                    ComponentName = definition.ComponentName,
                    ScopeType = "ApplicationShell",
                    ConfigJson = ApplicationShellMenuCatalog.FixedShellConfig(),
                    MenuType = definition.MenuType,
                    SortOrder = definition.SortOrder,
                    Visible = true,
                    PermissionCode = definition.PermissionCode,
                    Icon = definition.Icon,
                    CreatedBy = currentUserId,
                    CreatedTime = now,
                    IsDeleted = false
                });
                continue;
            }

            entity.MenuName = definition.MenuName;
            entity.ParentCode = definition.ParentCode;
            entity.RoutePath = definition.RoutePath;
            entity.ComponentName = definition.ComponentName;
            entity.ScopeType = "ApplicationShell";
            entity.ConfigJson = ApplicationShellMenuCatalog.FixedShellConfig();
            entity.MenuType = definition.MenuType;
            entity.SortOrder = definition.SortOrder;
            entity.Visible = true;
            entity.PermissionCode = definition.PermissionCode;
            entity.Icon = definition.Icon;
            entity.IsDeleted = false;
            entity.DeletedBy = null;
            entity.DeletedTime = null;
            entity.UpdatedBy = currentUserId;
            entity.UpdatedTime = now;
            updates.Add(entity);
        }

        if (inserts.Count > 0)
        {
            await appDb.Insertable(inserts).ExecuteCommandAsync(cancellationToken);
        }

        if (updates.Count > 0)
        {
            await appDb.Updateable(updates).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static async Task NormalizeRuntimeMenuRoutesAsync(
        ISqlSugarClient appDb,
        string tenantId,
        string appCode,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        var candidateMenus = await appDb.Queryable<SystemMenuEntity>()
            .Where(item =>
                item.TenantId == tenantId &&
                item.AppCode == normalizedAppCode &&
                item.ScopeType == "ApplicationRuntime" &&
                !item.IsDeleted &&
                item.PageCode != null &&
                item.PageCode != "")
            .ToListAsync(cancellationToken);
        var menus = candidateMenus
            .Where(item => item.RoutePath?.StartsWith("/runtime/", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        if (menus.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var menu in menus)
        {
            var pageCode = menu.PageCode?.Trim();
            if (string.IsNullOrWhiteSpace(pageCode))
            {
                continue;
            }

            menu.RoutePath = $"/pages/{Uri.EscapeDataString(pageCode)}";
            menu.ComponentName = "RuntimePage";
            menu.UpdatedBy = currentUserId;
            menu.UpdatedTime = now;
        }

        await appDb.Updateable(menus).ExecuteCommandAsync(cancellationToken);
    }

    private static async Task<bool> HasRuntimeMenuRoutesToNormalizeAsync(
        ISqlSugarClient appDb,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken)
    {
        if (!appDb.DbMaintenance.IsAnyTable("system_menus", false))
        {
            return true;
        }

        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        var candidateRoutes = await appDb.Queryable<SystemMenuEntity>()
            .Where(item =>
                item.TenantId == tenantId &&
                item.AppCode == normalizedAppCode &&
                item.ScopeType == "ApplicationRuntime" &&
                !item.IsDeleted &&
                item.PageCode != null &&
                item.PageCode != "")
            .Select(item => item.RoutePath)
            .ToListAsync(cancellationToken);

        return candidateRoutes.Any(route => route?.StartsWith("/runtime/", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static async Task<bool> HasRuntimeMenuDataModelAsync(
        ISqlSugarClient appDb,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken)
    {
        return await appDb.Queryable<SystemDataModelEntity>()
            .Where(item =>
                item.TenantId == tenantId &&
                item.AppCode == appCode.Trim().ToUpperInvariant() &&
                item.ModelCode == ApplicationRuntimeDataModelCatalog.RuntimeMenuModelCode &&
                item.Status == "Published" &&
                !item.IsDeleted)
            .AnyAsync(cancellationToken);
    }

    private static async Task UpsertRuntimeMenuDataModelAsync(
        ISqlSugarClient appDb,
        string tenantId,
        string appCode,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        var existing = (await appDb.Queryable<SystemDataModelEntity>()
            .Where(item =>
                item.TenantId == tenantId &&
                item.AppCode == normalizedAppCode &&
                item.ModelCode == ApplicationRuntimeDataModelCatalog.RuntimeMenuModelCode)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        if (existing is null)
        {
            await appDb.Insertable(new SystemDataModelEntity
            {
                Id = $"data-model-{tenantId}-{normalizedAppCode}-runtime-menu".ToLowerInvariant(),
                TenantId = tenantId,
                AppCode = normalizedAppCode,
                ModelCode = ApplicationRuntimeDataModelCatalog.RuntimeMenuModelCode,
                ModelName = ApplicationRuntimeDataModelCatalog.RuntimeMenuModelName,
                ProviderKey = ApplicationRuntimeDataModelCatalog.RuntimeMenuProviderKey,
                KeyField = ApplicationRuntimeDataModelCatalog.RuntimeMenuKeyField,
                PermissionCode = ApplicationRuntimeDataModelCatalog.RuntimeConfigurationPermission,
                VersionNo = 1,
                Status = "Published",
                SchemaJson = ApplicationRuntimeDataModelCatalog.RuntimeMenuSchemaJson,
                CreatedBy = currentUserId,
                Remark = "Application database runtime data model baseline"
            }).ExecuteCommandAsync(cancellationToken);
            return;
        }

        existing.ModelName = ApplicationRuntimeDataModelCatalog.RuntimeMenuModelName;
        existing.ProviderKey = ApplicationRuntimeDataModelCatalog.RuntimeMenuProviderKey;
        existing.KeyField = ApplicationRuntimeDataModelCatalog.RuntimeMenuKeyField;
        existing.PermissionCode = ApplicationRuntimeDataModelCatalog.RuntimeConfigurationPermission;
        existing.Status = "Published";
        existing.SchemaJson = ApplicationRuntimeDataModelCatalog.RuntimeMenuSchemaJson;
        existing.IsDeleted = false;
        existing.DeletedBy = null;
        existing.DeletedTime = null;
        existing.UpdatedBy = currentUserId;
        existing.UpdatedTime = DateTime.UtcNow;
        await appDb.Updateable(existing).ExecuteCommandAsync(cancellationToken);
    }

    private static async Task SoftDeleteRetiredMenusAsync(
        ISqlSugarClient appDb,
        string tenantId,
        string appCode,
        string currentUserId,
        IReadOnlyList<ApplicationShellMenuDefinition> activeShellMenuDefinitions,
        CancellationToken cancellationToken)
    {
        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        var activeMenuCodes = activeShellMenuDefinitions
            .Select(item => item.MenuCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var retiredMenuCodes = ApplicationShellMenuCatalog.OptionalItems
            .Select(item => item.MenuCode)
            .Append(RetiredAdminCenterMenuCode)
            .Append(RetiredProjectManagementMenuCode)
            .Where(menuCode => !activeMenuCodes.Contains(menuCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (retiredMenuCodes.Length == 0)
        {
            return;
        }

        var retiredMenus = await appDb.Queryable<SystemMenuEntity>()
            .Where(item =>
                item.TenantId == tenantId &&
                item.AppCode == normalizedAppCode &&
                retiredMenuCodes.Contains(item.MenuCode) &&
                !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (retiredMenus.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var entity in retiredMenus)
        {
            entity.Visible = false;
            entity.IsDeleted = true;
            entity.DeletedBy = currentUserId;
            entity.DeletedTime = now;
            entity.UpdatedBy = currentUserId;
            entity.UpdatedTime = now;
        }

        await appDb.Updateable(retiredMenus).ExecuteCommandAsync(cancellationToken);
    }

    private static async Task UpsertBaselineVersionAsync(
        ISqlSugarClient appDb,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var entity = (await appDb.Queryable<SystemParameterEntity>()
            .Where(item => item.ParamKey == BaselineParameterKey)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        var now = DateTime.UtcNow;
        if (entity is null)
        {
            await appDb.Insertable(new SystemParameterEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                ParamName = "应用库基线版本",
                ParamKey = BaselineParameterKey,
                ParamValue = BaselineVersion,
                Category = "application-shell",
                IsEnabled = true,
                CreatedBy = currentUserId,
                CreatedTime = now,
                IsDeleted = false
            }).ExecuteCommandAsync(cancellationToken);
            return;
        }

        entity.ParamName = "应用库基线版本";
        entity.ParamValue = BaselineVersion;
        entity.Category = "application-shell";
        entity.IsEnabled = true;
        entity.IsDeleted = false;
        entity.DeletedBy = null;
        entity.DeletedTime = null;
        entity.UpdatedBy = currentUserId;
        entity.UpdatedTime = now;
        await appDb.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    private static async Task UpsertCapabilitySignatureAsync(
        ISqlSugarClient appDb,
        string currentUserId,
        string signature,
        CancellationToken cancellationToken)
    {
        var entity = (await appDb.Queryable<SystemParameterEntity>()
            .Where(item => item.ParamKey == CapabilitySignatureParameterKey)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        var now = DateTime.UtcNow;
        if (entity is null)
        {
            await appDb.Insertable(new SystemParameterEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                ParamName = "应用 Shell 能力签名",
                ParamKey = CapabilitySignatureParameterKey,
                ParamValue = signature,
                Category = "application-shell",
                IsEnabled = true,
                CreatedBy = currentUserId,
                CreatedTime = now,
                IsDeleted = false
            }).ExecuteCommandAsync(cancellationToken);
            return;
        }

        entity.ParamName = "应用 Shell 能力签名";
        entity.ParamValue = signature;
        entity.Category = "application-shell";
        entity.IsEnabled = true;
        entity.IsDeleted = false;
        entity.DeletedBy = null;
        entity.DeletedTime = null;
        entity.UpdatedBy = currentUserId;
        entity.UpdatedTime = now;
        await appDb.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }
}
