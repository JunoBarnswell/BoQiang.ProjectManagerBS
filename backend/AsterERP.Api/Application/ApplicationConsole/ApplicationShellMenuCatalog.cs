using System.Text.Json;
using AsterERP.Contracts.System.Menus;
using AsterERP.Shared;

namespace AsterERP.Api.Application.ApplicationConsole;

public static class ApplicationShellMenuCatalog
{
    private const string FixedShellConfigJson = "{\"fixedShell\":true}";

    public static readonly IReadOnlyList<ApplicationShellMenuDefinition> CoreItems =
    [
        new("home", "首页", null, "/home", "ApplicationConsolePage", PermissionCodes.AppHomeView, "House", 1),
        new("app-console", "应用控制台", null, "/console", "ApplicationConsolePage", PermissionCodes.AppConsoleView, "PanelsTopLeft", 2),
        new("workbench", "工作台", null, "/workbench", "ApplicationConsolePage", PermissionCodes.AppWorkbenchView, "ChartColumn", 3),
        new("dev-center", "开发中心", null, "/development-center", "ApplicationConsolePage", PermissionCodes.AppDevelopmentCenterView, "Code2", 5),
        new("data-center", "数据中心", null, "/data-center", "ApplicationConsolePage", PermissionCodes.AppDataCenterView, "DatabaseZap", 6)
    ];

    public static readonly IReadOnlyList<ApplicationShellMenuDefinition> AsterSceneItems =
    [
        new("asterscene", "AsterScene", null, null, null, null, "Box", 28, "Directory"),
        new("asterscene:pricing", "Pricing", "asterscene", "/pricing", "AsterScenePricingPage", null, "CreditCard", 2804),
        new("asterscene:pricing:im-messages", "站内信", "asterscene:pricing", "/im/messages", "ImMessagesPage", PermissionCodes.ImConversationView, "MessageSquareText", 280502)
    ];

    public static readonly IReadOnlyList<ApplicationShellMenuDefinition> AiItems =
    [
        new("ai", "智能中心", null, null, null, null, "Sparkles", 7, "Directory"),
        new("ai:workbench", "AI 工作台", "ai", "/ai/workbench", "AiWorkbenchPage", PermissionCodes.AiWorkbenchView, "MessagesSquare", 701),
        new("ai:capability", "能力中心", "ai", "/ai/capability", "AiCapabilityCenterPage", PermissionCodes.AiCapabilityView, "Boxes", 702),
        new("ai:model-configs", "模型配置", "ai", "/ai/model-configs", "AiCapabilityCenterPage", PermissionCodes.AiModelView, "SlidersHorizontal", 703),
        new("ai:providers", "模型供应商", "ai", "/ai/providers", "AiCapabilityCenterPage", PermissionCodes.AiProviderView, "Cloud", 704),
        new("ai:settings", "设置中心", "ai", "/ai/settings", "AiSettingsPage", PermissionCodes.AiSettingsView, "Settings2", 705)
    ];

    public static readonly IReadOnlyList<ApplicationShellMenuDefinition> SystemAdministrationItems =
    [
        new("system", "系统设置", null, null, null, null, "Settings", 100, "Directory"),
        new("system:user", "用户管理", "system", "/system/users", "UsersPage", PermissionCodes.SystemUserQuery, "UserCog", 101),
        new("system:dept", "部门管理", "system", "/system/departments", "DepartmentsPage", PermissionCodes.SystemDeptQuery, "Building2", 102),
        new("system:position", "岗位管理", "system", "/system/positions", "PositionsPage", PermissionCodes.SystemPositionQuery, "BriefcaseBusiness", 103),
        new("system:menu", "菜单管理", "system", "/system/menus", "MenusPage", PermissionCodes.SystemMenuQuery, "FolderTree", 104),
        new("system:role", "角色管理", "system", "/system/roles", "RolesPage", PermissionCodes.SystemRoleQuery, "ShieldCheck", 105),
        new("system:dict", "字典管理", "system", "/system/dicts", "DictsPage", PermissionCodes.SystemDictQuery, "BookOpen", 106),
        new("system:parameter", "系统参数", "system", "/system/parameters", "ParametersPage", PermissionCodes.SystemParameterQuery, "SlidersHorizontal", 107),
        new("system:announcement", "通知公告", "system", "/system/announcements", "AnnouncementsPage", PermissionCodes.SystemAnnouncementQuery, "Megaphone", 108),
        new("system:operation-log", "操作日志", "system", "/system/operation-logs", "OperationLogsPage", PermissionCodes.SystemOperationLogQuery, "ScrollText", 109),
        new("system:login-log", "登录日志", "system", "/system/login-logs", "LoginLogsPage", PermissionCodes.SystemLoginLogQuery, "ClipboardList", 110),
        new("system:online-user", "在线用户", "system", "/system/online-users", "OnlineUsersPage", PermissionCodes.SystemOnlineUserQuery, "UsersRound", 111),
        new("system:scheduled-job", "任务调度", "system", "/system/scheduled-jobs", "ScheduledJobsPage", PermissionCodes.SystemScheduledJobQuery, "Clock", 112),
        new("system:abp-setting", "ABP 基础设施", "system", "/system/abp-infrastructure-settings", "AbpInfrastructureSettingsPage", PermissionCodes.SystemAbpSettingQuery, "CircleHelp", 113),
        new("system:file", "文件中心", "system", "/system/files", "FilesPage", PermissionCodes.SystemFileQuery, "FileSearch", 114),
        new("system:print", "打印中心", "system", "/system/print-center", "PrintCenterPage", PermissionCodes.SystemPrintQuery, "Printer", 115)
    ];

    public static readonly IReadOnlyList<ApplicationShellMenuDefinition> WorkflowItems = ApplicationWorkflowShellMenuCatalog.Items;

    public static readonly IReadOnlyList<ApplicationShellMenuDefinition> OptionalItems =
    [
        .. AsterSceneItems,
        .. AiItems,
        .. SystemAdministrationItems,
        .. WorkflowItems
    ];

    public static readonly IReadOnlyList<ApplicationShellMenuDefinition> Items = CoreItems;

    public static IReadOnlyList<MenuTreeNodeResponse> BuildVisibleMenuTree(
        string tenantId,
        string appCode,
        IReadOnlyCollection<string> permissionCodes,
        IReadOnlySet<string>? shellCapabilities = null)
    {
        var normalizedTenantId = tenantId.Trim();
        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        var items = GetItems(shellCapabilities);
        var nodes = items
            .OrderBy(item => item.SortOrder)
            .Select(item => ToNode(item, normalizedTenantId, normalizedAppCode))
            .ToList();

        return FilterTree(BuildTree(nodes, items), permissionCodes);
    }

    public static IReadOnlyList<MenuTreeNodeResponse> BuildFullMenuTree(
        string tenantId,
        string appCode,
        IReadOnlySet<string>? shellCapabilities = null)
    {
        var normalizedTenantId = tenantId.Trim();
        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        var items = GetItems(shellCapabilities);
        var nodes = items
            .OrderBy(item => item.SortOrder)
            .Select(item => ToNode(item, normalizedTenantId, normalizedAppCode))
            .ToList();

        return BuildTree(nodes, items);
    }

    public static string FixedShellConfig() => FixedShellConfigJson;

    public static IReadOnlyList<ApplicationShellMenuDefinition> GetItems(IReadOnlySet<string>? shellCapabilities)
    {
        var items = new List<ApplicationShellMenuDefinition>(CoreItems);
        if (HasCapability(shellCapabilities, ApplicationShellCapabilityResolver.AsterSceneCapability))
        {
            items.AddRange(AsterSceneItems);
        }

        if (HasCapability(shellCapabilities, ApplicationShellCapabilityResolver.AiCapability))
        {
            items.AddRange(AiItems);
        }

        if (HasCapability(shellCapabilities, ApplicationShellCapabilityResolver.SystemAdministrationCapability))
        {
            items.AddRange(SystemAdministrationItems);
        }

        if (HasCapability(shellCapabilities, ApplicationShellCapabilityResolver.WorkflowCapability))
        {
            items.AddRange(WorkflowItems);
        }

        return items;
    }

    private static MenuTreeNodeResponse ToNode(
        ApplicationShellMenuDefinition item,
        string tenantId,
        string appCode)
    {
        return new MenuTreeNodeResponse(
            $"fixed:{tenantId}:{appCode}:{item.MenuCode}",
            tenantId,
            appCode,
            item.MenuName,
            item.MenuCode,
            item.RoutePath,
            item.ComponentName,
            null,
            null,
            "ApplicationShell",
            FixedShellConfigJson,
            item.MenuType,
            item.SortOrder,
            true,
            item.PermissionCode,
            item.Icon,
            []);
    }

    private static IReadOnlyList<MenuTreeNodeResponse> BuildTree(
        IReadOnlyList<MenuTreeNodeResponse> nodes,
        IReadOnlyList<ApplicationShellMenuDefinition> items)
    {
        var builders = nodes.ToDictionary(node => node.MenuCode, node => new ShellTreeNodeBuilder(node), StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ParentCode) ||
                !builders.TryGetValue(item.MenuCode, out var node) ||
                !builders.TryGetValue(item.ParentCode, out var parent))
            {
                continue;
            }

            parent.Children.Add(node);
        }

        return items
            .Where(item => string.IsNullOrWhiteSpace(item.ParentCode) || !builders.ContainsKey(item.ParentCode))
            .OrderBy(item => item.SortOrder)
            .Select(item => builders[item.MenuCode].ToResponse())
            .ToList();
    }

    private static bool HasCapability(IReadOnlySet<string>? shellCapabilities, string capability) =>
        shellCapabilities?.Contains(capability) == true;

    private static IReadOnlyList<MenuTreeNodeResponse> FilterTree(
        IReadOnlyList<MenuTreeNodeResponse> nodes,
        IReadOnlyCollection<string> permissionCodes)
    {
        if (permissionCodes.Contains("*", StringComparer.OrdinalIgnoreCase))
        {
            return nodes;
        }

        var filtered = new List<MenuTreeNodeResponse>();
        foreach (var node in nodes)
        {
            var children = FilterTree(node.Children, permissionCodes);
            var permitted = string.IsNullOrWhiteSpace(node.PermissionCode) ||
                permissionCodes.Contains(node.PermissionCode, StringComparer.OrdinalIgnoreCase);
            if (permitted || children.Count > 0)
            {
                filtered.Add(node with { Children = children.ToList() });
            }
        }

        return filtered;
    }

    public static string BuildFixedShellConfigJson()
    {
        return JsonSerializer.Serialize(new { fixedShell = true });
    }

    private sealed class ShellTreeNodeBuilder(MenuTreeNodeResponse value)
    {
        public MenuTreeNodeResponse Value { get; } = value;

        public List<ShellTreeNodeBuilder> Children { get; } = [];

        public MenuTreeNodeResponse ToResponse()
        {
            var children = Children
                .OrderBy(item => item.Value.SortOrder)
                .Select(item => item.ToResponse())
                .ToList();

            return Value with { Children = children };
        }
    }
}
