using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Shared;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.AsterScene;

public sealed class AsterSceneSeedService(ISqlSugarClient db)
{
    private const string ModuleName = "AsterScene";

    public Task SeedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SeedPermissions();
        SeedMenus();
        return Task.CompletedTask;
    }

    private void SeedPermissions()
    {
        var permissions = new[]
        {
            (PermissionCodes.AsterSceneProjectView, "AsterScene project view"), (PermissionCodes.AsterSceneProjectList, "AsterScene project list"),
            (PermissionCodes.AsterSceneProjectCreate, "AsterScene project create"), (PermissionCodes.AsterSceneProjectEdit, "AsterScene project edit"),
            (PermissionCodes.AsterSceneProjectDelete, "AsterScene project delete"), (PermissionCodes.AsterSceneStudioOpen, "AsterScene studio open"),
            (PermissionCodes.AsterSceneDocumentSave, "AsterScene document save"), (PermissionCodes.AsterSceneAssetView, "AsterScene asset view"),
            (PermissionCodes.AsterSceneAssetUpload, "AsterScene asset upload"), (PermissionCodes.AsterSceneAssetDelete, "AsterScene asset delete"),
            (PermissionCodes.AsterSceneJobView, "AsterScene job view"), (PermissionCodes.AsterScenePublishView, "AsterScene publish view"),
            (PermissionCodes.AsterScenePublishExecute, "AsterScene publish execute"), (PermissionCodes.AsterScenePublishRollback, "AsterScene publish rollback"),
            (PermissionCodes.AsterSceneCommunityInteract, "AsterScene community interact"), (PermissionCodes.AsterSceneRemixCreate, "AsterScene remix create"),
            (PermissionCodes.AsterSceneSubscriptionManage, "AsterScene subscription manage"), (PermissionCodes.AsterSceneUsageView, "AsterScene usage view"),
            (PermissionCodes.AsterSceneAiGenerate, "AsterScene AI generate"), (PermissionCodes.AsterSceneSupportCreate, "AsterScene support create"),
            (PermissionCodes.AsterSceneSupportView, "AsterScene support view"), (PermissionCodes.AsterSceneSupportComment, "AsterScene support comment"),
            (PermissionCodes.AsterSceneSupportClose, "AsterScene support close"), (PermissionCodes.AsterSceneAdminView, "AsterScene admin view"),
            (PermissionCodes.AsterSceneModerationManage, "AsterScene moderation manage")
            , (PermissionCodes.AsterSceneModerationAppealCreate, "AsterScene moderation appeal create")
            , (PermissionCodes.AsterSceneAppealManage, "AsterScene appeal manage")
            , (PermissionCodes.AsterSceneSupportAdminView, "AsterScene support admin view")
            , (PermissionCodes.AsterSceneSupportAdminManage, "AsterScene support admin manage")
        };
        foreach (var (code, name) in permissions)
        {
            var existing = db.Queryable<SystemPermissionCodeEntity>().First(item => item.PermissionCode == code);
            if (existing is null)
            {
                db.Insertable(new SystemPermissionCodeEntity { ModuleName = ModuleName, PermissionCode = code, PermissionName = name, IsEnabled = true }).ExecuteCommand();
            }
            else
            {
                existing.ModuleName = ModuleName; existing.PermissionName = name; existing.IsEnabled = true; existing.IsDeleted = false; existing.UpdatedTime = DateTime.UtcNow;
                db.Updateable(existing).ExecuteCommand();
            }
        }
    }

    private void SeedMenus()
    {
        var tenantApps = db.Queryable<SystemTenantAppEntity>().Where(item => !item.IsDeleted && item.Status == "Enabled" && item.AppCode == "SYSTEM").ToList();
        foreach (var tenantApp in tenantApps)
        {
            var appCode = tenantApp.AppCode.Trim().ToUpperInvariant();
            UpsertMenu(tenantApp.TenantId, appCode, "asterscene", "AsterScene", null, null, null, "Directory", 28, true, null, "ph ph-cube-focus");
            UpsertMenu(tenantApp.TenantId, appCode, "asterscene:explore", "Explore", "asterscene", "/explore", "AsterSceneExplorePage", "Menu", 1, true, null, "ph ph-compass");
            UpsertMenu(tenantApp.TenantId, appCode, "asterscene:dashboard", "Creator Dashboard", "asterscene", "/dashboard", "AsterSceneDashboardPage", "Menu", 2, true, PermissionCodes.AsterSceneProjectList, "ph ph-gauge");
            UpsertMenu(tenantApp.TenantId, appCode, "asterscene:assets", "Assets", "asterscene", "/assets", "AsterSceneAssetsPage", "Menu", 3, true, PermissionCodes.AsterSceneAssetView, "ph ph-package");
            UpsertMenu(tenantApp.TenantId, appCode, "asterscene:pricing", "Pricing", "asterscene", "/pricing", "AsterScenePricingPage", "Menu", 4, true, null, "ph ph-credit-card");
            UpsertMenu(tenantApp.TenantId, appCode, "asterscene:admin", "AsterScene Admin", "asterscene", "/admin/asterscene", "AsterSceneAdminPage", "Menu", 5, true, PermissionCodes.AsterSceneAdminView, "ph ph-shield-check");
            UpsertMenu(tenantApp.TenantId, appCode, "asterscene:project:create", "Create project", "asterscene:dashboard", null, null, "Button", 101, false, PermissionCodes.AsterSceneProjectCreate, null);
            UpsertMenu(tenantApp.TenantId, appCode, "asterscene:document:save", "Save document", "asterscene:dashboard", null, null, "Button", 102, false, PermissionCodes.AsterSceneDocumentSave, null);
            UpsertMenu(tenantApp.TenantId, appCode, "asterscene:asset:upload", "Upload asset", "asterscene:assets", null, null, "Button", 201, false, PermissionCodes.AsterSceneAssetUpload, null);
            UpsertMenu(tenantApp.TenantId, appCode, "asterscene:publish:execute", "Publish", "asterscene:dashboard", null, null, "Button", 301, false, PermissionCodes.AsterScenePublishExecute, null);
            UpsertMenu(tenantApp.TenantId, appCode, "asterscene:publish:rollback", "Rollback publish", "asterscene:dashboard", null, null, "Button", 302, false, PermissionCodes.AsterScenePublishRollback, null);
            UpsertMenu(tenantApp.TenantId, appCode, "asterscene:remix:create", "Create Remix", "asterscene:explore", null, null, "Button", 401, false, PermissionCodes.AsterSceneRemixCreate, null);
            UpsertMenu(tenantApp.TenantId, appCode, "asterscene:subscription:manage", "Manage subscription", "asterscene:pricing", null, null, "Button", 501, false, PermissionCodes.AsterSceneSubscriptionManage, null);
            UpsertMenu(tenantApp.TenantId, appCode, "asterscene:moderation:manage", "Moderate", "asterscene:admin", null, null, "Button", 601, false, PermissionCodes.AsterSceneModerationManage, null);
        }
    }

    private void UpsertMenu(string tenantId, string appCode, string menuCode, string menuName, string? parentCode, string? routePath, string? componentName, string menuType, int sortOrder, bool visible, string? permissionCode, string? icon)
    {
        var existing = db.Queryable<SystemMenuEntity>().First(item => item.TenantId == tenantId && item.AppCode == appCode && item.MenuCode == menuCode);
        if (existing is null)
        {
            db.Insertable(new SystemMenuEntity { TenantId = tenantId, AppCode = appCode, MenuCode = menuCode, MenuName = menuName, ParentCode = parentCode, RoutePath = routePath, ComponentName = componentName, MenuType = menuType, SortOrder = sortOrder, Visible = visible, PermissionCode = permissionCode, Icon = icon }).ExecuteCommand();
            return;
        }
        existing.MenuName = menuName; existing.ParentCode = parentCode; existing.RoutePath = routePath; existing.ComponentName = componentName; existing.MenuType = menuType; existing.SortOrder = sortOrder; existing.Visible = visible; existing.PermissionCode = permissionCode; existing.Icon = icon; existing.IsDeleted = false; existing.UpdatedTime = DateTime.UtcNow;
        db.Updateable(existing).ExecuteCommand();
    }
}
