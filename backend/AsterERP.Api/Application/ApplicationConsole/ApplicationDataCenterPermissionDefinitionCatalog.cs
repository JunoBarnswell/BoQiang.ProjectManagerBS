using AsterERP.Shared;

namespace AsterERP.Api.Application.ApplicationConsole;

public static class ApplicationDataCenterPermissionDefinitionCatalog
{
    private static readonly IReadOnlyList<(string Code, string Name)> Modules =
    [
        ("data-source", "数据源管理"),
        ("connection-test", "连接测试"),
        ("data-model", "数据模型"),
        ("microflow", "微流"),
        ("api-service", "API 服务"),
        ("entity-field", "实体字段"),
        ("dictionary-code", "字典编码"),
        ("mapping-cache", "映射缓存"),
        ("query-dataset", "查询数据集"),
        ("integration-task", "集成任务")
    ];

    private static readonly IReadOnlyList<(string Action, string Name)> Actions =
    [
        ("view", "查看"),
        ("add", "新增"),
        ("edit", "编辑"),
        ("delete", "删除"),
        ("enable", "启用"),
        ("disable", "停用"),
        ("test", "测试"),
        ("preview", "预览"),
        ("data-query", "查询数据"),
        ("data-edit", "编辑数据"),
        ("import", "导入"),
        ("export", "导出"),
        ("publish", "发布"),
        ("reference", "引用分析"),
        ("refresh", "刷新")
    ];

    public static readonly IReadOnlyList<ApplicationShellPermissionDefinition> Definitions = BuildDefinitions();

    private static IReadOnlyList<ApplicationShellPermissionDefinition> BuildDefinitions()
    {
        var definitions = new List<ApplicationShellPermissionDefinition>();
        var allowed = PermissionCodes.AppDataCenterPermissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var module in Modules)
        {
            foreach (var action in Actions)
            {
                var code = $"app:data-center:{module.Code}:{action.Action}";
                if (allowed.Contains(code))
                {
                    definitions.Add(new ApplicationShellPermissionDefinition("ApplicationDataCenter", code, $"{module.Name}{action.Name}"));
                }
            }
        }

        return definitions;
    }
}
