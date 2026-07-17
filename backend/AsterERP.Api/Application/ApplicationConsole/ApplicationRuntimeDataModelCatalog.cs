using System.Text.Json;

namespace AsterERP.Api.Application.ApplicationConsole;

public static class ApplicationRuntimeDataModelCatalog
{
    public const string RuntimeMenuModelCode = "runtime.menu";
    public const string RuntimeMenuModelName = "菜单配置";
    public const string RuntimeMenuProviderKey = "system.menus";
    public const string RuntimeMenuKeyField = "id";
    public const string RuntimeConfigurationPermission = "runtime:configuration:query";

    public static string RuntimeMenuSchemaJson => JsonSerializer.Serialize(new
    {
        fields = new object[]
        {
            BuildDataField("id", "ID", "text", "id", false, false, false, false, "text", null, "220px", null, 0),
            BuildDataField("menuName", "菜单名称", "text", "menuName", true, true, true, true, "text", null, "180px", null, 1),
            BuildDataField("menuCode", "菜单编码", "text", "menuCode", true, true, true, true, "text", null, "220px", null, 2),
            BuildDataField("parentCode", "上级编码", "text", "parentCode", true, true, true, true, "text", null, "160px", null, 3),
            BuildDataField("routePath", "路由", "text", "routePath", true, true, false, true, "text", null, "220px", null, 4),
            BuildDataField("pageCode", "页面编码", "text", "pageCode", true, true, true, true, "text", null, "190px", null, 5),
            BuildDataField("menuType", "菜单类型", "text", "menuType", true, true, true, true, "tag", null, "110px", null, 6),
            BuildDataField("sortOrder", "排序", "number", "sortOrder", true, true, true, true, "number", null, "90px", null, 7),
            BuildDataField("visible", "可见", "boolean", "visible", true, true, true, true, "tag", null, "90px", null, 8),
            BuildDataField("permissionCode", "权限码", "text", "permissionCode", true, true, true, true, "text", null, "200px", null, 9),
            BuildDataField("icon", "图标", "text", "icon", true, true, false, true, "text", null, "130px", null, 10),
            BuildDataField("createdTime", "创建时间", "datetime", "createdTime", true, false, true, true, "datetime", null, "190px", null, 11),
            BuildDataField("updatedTime", "更新时间", "datetime", "updatedTime", true, false, true, true, "datetime", null, "190px", null, 12)
        }
    });

    private static object BuildDataField(
        string fieldCode,
        string fieldName,
        string dataType,
        string binding,
        bool visible,
        bool queryable,
        bool sortable,
        bool exportable,
        string? renderer,
        string? dictType,
        string? width,
        string? fixedSide,
        int order,
        bool writable = false) =>
        new
        {
            fieldCode,
            fieldName,
            dataType,
            binding,
            visible,
            queryable,
            sortable,
            exportable,
            writable,
            renderer,
            dictType,
            width,
            @fixed = fixedSide,
            order
        };
}
