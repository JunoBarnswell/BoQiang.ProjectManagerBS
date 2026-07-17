using System.Text.Json.Nodes;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Contracts.System.Printing;
using AsterERP.Api.Modules.System.Menus;
using SqlSugar;

namespace AsterERP.Api.Application.System.Printing;

public sealed class PrintTargetCatalog(
    IWorkspaceDatabaseAccessor databaseAccessor,
    PrintWorkspaceResolver workspaceResolver)
{
    private static readonly IReadOnlyDictionary<string, PrintTargetDefinition> Definitions =
        new Dictionary<string, PrintTargetDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["system:user"] = new(
                "system:user",
                "用户打印",
                true,
                [
                    new PrintTargetSceneDefinition("list", "system_user_default", null, CreateUserListTestData),
                    new PrintTargetSceneDefinition("detail", null, "system.user.detail", CreateUserDetailTestData)
                ]),
            ["system:role"] = new(
                "system:role",
                "角色打印",
                true,
                [
                    new PrintTargetSceneDefinition("list", "system_role_default", null, CreateRoleListTestData),
                    new PrintTargetSceneDefinition("detail", null, "system.role.detail", CreateRoleDetailTestData)
                ]),
            ["system:file"] = new(
                "system:file",
                "文件打印",
                true,
                [
                    new PrintTargetSceneDefinition("list", "system_file_default", null, CreateFileListTestData),
                    new PrintTargetSceneDefinition("detail", null, "system.file.detail", CreateFileDetailTestData)
                ])
        };

    public async Task<IReadOnlyList<PrintTargetOptionResponse>> GetTargetsAsync(CancellationToken cancellationToken = default)
    {
        var scope = workspaceResolver.GetRequiredCurrent();
        var menuCodes = Definitions.Keys.ToList();
        var menus = await databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TenantId == scope.TenantId &&
                item.AppCode == scope.AppCode &&
                menuCodes.Contains(item.MenuCode))
            .ToListAsync(cancellationToken);

        return Definitions.Values
            .Select(definition =>
            {
                var menu = menus.FirstOrDefault(item => string.Equals(item.MenuCode, definition.MenuCode, StringComparison.OrdinalIgnoreCase));
                return new PrintTargetOptionResponse(
                    definition.MenuCode,
                    menu?.MenuName ?? definition.MenuCode,
                    menu?.RoutePath,
                    definition.DefaultTitle,
                    definition.SupportsAssets,
                    definition.Scenes.Select(item => item.Scene).ToList());
            })
            .ToList();
    }

    public async Task<PrintTargetDetailResponse> GetTargetDetailAsync(string menuCode, string? scene, CancellationToken cancellationToken = default)
    {
        var definition = GetRequiredDefinition(menuCode);
        var sceneDefinition = GetRequiredScene(definition, scene);
        var scope = workspaceResolver.GetRequiredCurrent();
        var menu = await databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TenantId == scope.TenantId &&
                item.AppCode == scope.AppCode &&
                item.MenuCode == definition.MenuCode)
            .FirstAsync(cancellationToken);

        var testData = sceneDefinition.CreateTestData();
        return new PrintTargetDetailResponse(
            definition.MenuCode,
            menu?.MenuName ?? definition.MenuCode,
            menu?.RoutePath,
            definition.DefaultTitle,
            definition.SupportsAssets,
            definition.Scenes.Select(item => item.Scene).ToList(),
            sceneDefinition.Scene,
            sceneDefinition.ListViewCode,
            sceneDefinition.DetailProviderKey,
            testData.DeepClone(),
            PrintVariableTreeBuilder.Build(testData));
    }

    public PrintTargetDefinition GetRequiredDefinition(string menuCode)
    {
        if (Definitions.TryGetValue(menuCode.Trim(), out var definition))
        {
            return definition;
        }

        throw new KeyNotFoundException($"未配置打印目标：{menuCode}");
    }

    public PrintTargetSceneDefinition GetRequiredScene(PrintTargetDefinition definition, string? scene)
    {
        var normalizedScene = string.IsNullOrWhiteSpace(scene) ? definition.Scenes[0].Scene : scene.Trim().ToLowerInvariant();
        var sceneDefinition = definition.Scenes.FirstOrDefault(item => string.Equals(item.Scene, normalizedScene, StringComparison.OrdinalIgnoreCase));
        return sceneDefinition ?? throw new KeyNotFoundException($"打印目标 {definition.MenuCode} 不支持场景：{scene}");
    }

    private static JsonObject CreateMeta(string menuCode, string scene, string title)
    {
        return new JsonObject
        {
            ["menuCode"] = menuCode,
            ["scene"] = scene,
            ["title"] = title,
            ["printedAt"] = "2026-06-27T12:00:00Z"
        };
    }

    private static JsonObject CreateSummary(string mode, int total, int selectedCount, int pageIndex, int pageSize)
    {
        return new JsonObject
        {
            ["mode"] = mode,
            ["total"] = total,
            ["selectedCount"] = selectedCount,
            ["pageIndex"] = pageIndex,
            ["pageSize"] = pageSize
        };
    }

    private static JsonObject CreateUserListTestData() =>
        new()
        {
            ["meta"] = CreateMeta("system:user", "list", "用户列表打印"),
            ["summary"] = CreateSummary("currentPage", 1, 0, 1, 20),
            ["rows"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "user-demo-001",
                    ["userName"] = "admin",
                    ["displayName"] = "系统管理员",
                    ["phoneNumber"] = "13800000000",
                    ["email"] = "admin@astererp.local",
                    ["deptId"] = "dept-001",
                    ["deptName"] = "平台中心",
                    ["positionId"] = "position-001",
                    ["positionName"] = "管理员",
                    ["isAdmin"] = true,
                    ["status"] = "Enabled",
                    ["dataScope"] = "ALL",
                    ["roleIds"] = new JsonArray("role-admin"),
                    ["roleNames"] = new JsonArray("管理员"),
                    ["remark"] = "演示用户"
                }
            }
        };

    private static JsonObject CreateUserDetailTestData() =>
        new()
        {
            ["meta"] = CreateMeta("system:user", "detail", "用户详情打印"),
            ["detail"] = CreateUserListTestData()["rows"]?[0]?.DeepClone()
        };

    private static JsonObject CreateRoleListTestData() =>
        new()
        {
            ["meta"] = CreateMeta("system:role", "list", "角色列表打印"),
            ["summary"] = CreateSummary("currentPage", 1, 0, 1, 20),
            ["rows"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "role-demo-001",
                    ["tenantId"] = "tenant-system",
                    ["appCode"] = "SYSTEM",
                    ["roleName"] = "管理员",
                    ["roleCode"] = "ADMIN",
                    ["dataScope"] = "ALL",
                    ["isEnabled"] = true,
                    ["userCount"] = 3,
                    ["permissionCount"] = 120,
                    ["remark"] = "演示角色"
                }
            }
        };

    private static JsonObject CreateRoleDetailTestData() =>
        new()
        {
            ["meta"] = CreateMeta("system:role", "detail", "角色详情打印"),
            ["detail"] = CreateRoleListTestData()["rows"]?[0]?.DeepClone()
        };

    private static JsonObject CreateFileListTestData() =>
        new()
        {
            ["meta"] = CreateMeta("system:file", "list", "文件列表打印"),
            ["summary"] = CreateSummary("currentPage", 1, 0, 1, 20),
            ["rows"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "file-demo-001",
                    ["fileName"] = "company-logo.png",
                    ["contentType"] = "image/png",
                    ["size"] = 20480,
                    ["relativePath"] = "assets/company-logo.png",
                    ["createdTime"] = "2026-06-27T12:00:00Z",
                    ["remark"] = "演示素材",
                    ["extension"] = "png",
                    ["downloadUrl"] = "/api/system/files/file-demo-001/download",
                    ["previewUrl"] = "/api/system/files/file-demo-001/preview",
                    ["previewSupported"] = true,
                    ["previewCategory"] = "image",
                    ["previewType"] = "image",
                    ["previewPipeline"] = "native"
                }
            }
        };

    private static JsonObject CreateFileDetailTestData() =>
        new()
        {
            ["meta"] = CreateMeta("system:file", "detail", "文件详情打印"),
            ["detail"] = CreateFileListTestData()["rows"]?[0]?.DeepClone()
        };
}

