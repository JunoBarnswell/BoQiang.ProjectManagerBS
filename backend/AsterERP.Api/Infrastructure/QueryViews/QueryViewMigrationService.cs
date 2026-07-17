using System.Text.Json.Nodes;
using AsterERP.Api.Modules.System.QueryViews;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.QueryViews;

public sealed class QueryViewMigrationService(ISqlSugarClient db)
{
    private static readonly ISet<string> NonQueryableProjectionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "system_dict_item:dictCode",
        "system_dict_item:dictName",
        "system_position_default:deptName"
    };

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await MigrateDatabaseAsync(db, cancellationToken);
    }

    public static async Task MigrateDatabaseAsync(ISqlSugarClient targetDb, CancellationToken cancellationToken = default)
    {
        targetDb.CodeFirst.InitTables(
            typeof(SystemQueryViewTableResourceEntity),
            typeof(SystemQueryViewColumnResourceEntity),
            typeof(SystemQueryViewDefinitionEntity),
            typeof(SystemQueryViewPublishLogEntity),
            typeof(SystemQueryViewRuntimeEntity),
            typeof(SystemQueryViewUserPreferenceEntity),
            typeof(SystemQueryViewExportTaskEntity));

        await new QueryViewMigrationService(targetDb).SeedDefaultDefinitionsAsync(cancellationToken);
    }

    private async Task SeedDefaultDefinitionsAsync(CancellationToken cancellationToken)
    {
        await UpsertDefaultDefinitionAsync("system_user_default", "用户管理默认视图", "system:user", "list", 5000, NormalizeSystemUserDesignJson("""
{"viewName":"用户管理默认视图","viewCode":"system_user_default","moduleCode":"system","menuCode":"system:user","viewType":"list","isDefault":true,"isEnabled":true,"defaultPageSize":20,"maxPageSize":5000,"remark":"系统默认查询视图","tables":[],"relations":[],"projections":[{"columnResourceId":"","fieldCode":"id","displayName":"ID","fieldAlias":"id","dataType":"string","width":120,"align":"left","isVisible":false,"isQueryable":true,"isSortable":true,"isExportable":false,"isFrozen":false,"dictType":null,"maskRule":null,"permissionCode":null},{"columnResourceId":"","fieldCode":"userName","displayName":"用户名","fieldAlias":"userName","dataType":"string","width":160,"align":"left","isVisible":true,"isQueryable":true,"isSortable":true,"isExportable":true,"isFrozen":false,"dictType":null,"maskRule":null,"permissionCode":null},{"columnResourceId":"","fieldCode":"displayName","displayName":"用户姓名","fieldAlias":"displayName","dataType":"string","width":160,"align":"left","isVisible":true,"isQueryable":true,"isSortable":true,"isExportable":true,"isFrozen":false,"dictType":null,"maskRule":null,"permissionCode":null},{"columnResourceId":"","fieldCode":"deptId","displayName":"部门ID","fieldAlias":"deptId","dataType":"string","width":120,"align":"left","isVisible":false,"isQueryable":true,"isSortable":false,"isExportable":false,"isFrozen":false,"dictType":null,"maskRule":null,"permissionCode":null},{"columnResourceId":"","fieldCode":"deptName","displayName":"部门","fieldAlias":"deptName","dataType":"string","width":160,"align":"left","isVisible":true,"isQueryable":true,"isSortable":true,"isExportable":true,"isFrozen":false,"dictType":null,"maskRule":null,"permissionCode":null},{"columnResourceId":"","fieldCode":"positionId","displayName":"岗位ID","fieldAlias":"positionId","dataType":"string","width":120,"align":"left","isVisible":false,"isQueryable":true,"isSortable":false,"isExportable":false,"isFrozen":false,"dictType":null,"maskRule":null,"permissionCode":null},{"columnResourceId":"","fieldCode":"positionName","displayName":"岗位","fieldAlias":"positionName","dataType":"string","width":140,"align":"left","isVisible":true,"isQueryable":true,"isSortable":true,"isExportable":true,"isFrozen":false,"dictType":null,"maskRule":null,"permissionCode":null},{"columnResourceId":"","fieldCode":"phoneNumber","displayName":"手机","fieldAlias":"phoneNumber","dataType":"string","width":150,"align":"left","isVisible":true,"isQueryable":true,"isSortable":false,"isExportable":true,"isFrozen":false,"dictType":null,"maskRule":"phone","permissionCode":null},{"columnResourceId":"","fieldCode":"email","displayName":"邮箱","fieldAlias":"email","dataType":"string","width":180,"align":"left","isVisible":false,"isQueryable":true,"isSortable":false,"isExportable":true,"isFrozen":false,"dictType":null,"maskRule":null,"permissionCode":null},{"columnResourceId":"","fieldCode":"roleNames","displayName":"角色","fieldAlias":"roleNames","dataType":"string","width":180,"align":"left","isVisible":true,"isQueryable":false,"isSortable":false,"isExportable":true,"isFrozen":false,"dictType":null,"maskRule":null,"permissionCode":null},{"columnResourceId":"","fieldCode":"status","displayName":"状态","fieldAlias":"status","dataType":"string","width":90,"align":"center","isVisible":true,"isQueryable":true,"isSortable":true,"isExportable":true,"isFrozen":false,"dictType":null,"maskRule":null,"permissionCode":null},{"columnResourceId":"","fieldCode":"createdTime","displayName":"创建时间","fieldAlias":"createdTime","dataType":"date","width":170,"align":"left","isVisible":false,"isQueryable":true,"isSortable":true,"isExportable":true,"isFrozen":false,"dictType":null,"maskRule":null,"permissionCode":null},{"columnResourceId":"","fieldCode":"remark","displayName":"备注","fieldAlias":"remark","dataType":"string","width":180,"align":"left","isVisible":false,"isQueryable":true,"isSortable":false,"isExportable":true,"isFrozen":false,"dictType":null,"maskRule":null,"permissionCode":null}],"conditions":[{"fieldCode":"userName","controlType":"Input","operator":"contains","isDefault":false,"defaultValue":null},{"fieldCode":"status","controlType":"Select","operator":"eq","isDefault":false,"defaultValue":null},{"fieldCode":"deptId","controlType":"DeptSelect","operator":"eq","isDefault":false,"defaultValue":null}],"sorts":[{"fieldCode":"createdTime","direction":"desc","sortOrder":1}]}
"""), cancellationToken);

        await UpsertDefaultDefinitionAsync("system_dept_tree", "部门管理树视图", "system:dept", "tree", 5000, DefaultDesignJson("system_dept_tree", "部门管理树视图", "system:dept", "tree", ("id", "ID", false, true), ("parentId", "上级ID", false, true), ("deptName", "部门名称", true, true), ("deptCode", "部门编码", true, true), ("parentName", "上级部门", true, false), ("managerName", "负责人", true, true), ("phoneNumber", "联系电话", true, true), ("sortOrder", "排序", true, true), ("status", "状态", true, true), ("createdTime", "创建时间", false, true), ("remark", "备注", false, false)), cancellationToken);
        await UpsertDefaultDefinitionAsync("system_position_default", "岗位管理默认视图", "system:position", "list", 5000, DefaultDesignJson("system_position_default", "岗位管理默认视图", "system:position", "list", ("id", "ID", false, true), ("positionName", "岗位名称", true, true), ("positionCode", "岗位编码", true, true), ("deptId", "部门ID", false, true), ("deptName", "所属部门", true, true), ("positionLevel", "岗位级别", false, true), ("sortOrder", "排序", true, true), ("status", "状态", true, true), ("createdTime", "创建时间", false, true), ("remark", "备注", true, false)), cancellationToken);
        await UpsertDefaultDefinitionAsync("system_menu_tree", "菜单管理树视图", "system:menu", "tree", 5000, DefaultDesignJson("system_menu_tree", "菜单管理树视图", "system:menu", "tree", ("id", "ID", false, true), ("tenantId", "租户ID", false, true), ("appCode", "应用编码", false, true), ("menuName", "菜单名称", true, true), ("menuCode", "菜单编码", true, true), ("parentCode", "父级编码", false, true), ("parentMenuName", "上级菜单", true, false), ("routePath", "路由", true, true), ("componentName", "组件", true, true), ("pageCode", "页面编码", false, true), ("menuType", "类型", true, true), ("sortOrder", "排序", true, true), ("visible", "可见", true, true), ("permissionCode", "权限码", true, true), ("icon", "图标", false, false), ("createdTime", "创建时间", false, true), ("remark", "备注", false, false)), cancellationToken);
        await UpsertDefaultDefinitionAsync("system_role_default", "角色管理默认视图", "system:role", "list", 5000, DefaultDesignJson("system_role_default", "角色管理默认视图", "system:role", "list", ("id", "ID", false, true), ("tenantId", "租户ID", false, true), ("appCode", "应用编码", false, true), ("roleName", "角色名称", true, true), ("roleCode", "角色编码", true, true), ("dataScope", "数据范围", true, true), ("isEnabled", "启用", true, true), ("createdTime", "创建时间", false, true), ("remark", "备注", true, false)), cancellationToken);
        await UpsertDefaultDefinitionAsync("system_file_default", "文件中心默认视图", "system:file", "list", 5000, DefaultDesignJson("system_file_default", "文件中心默认视图", "system:file", "list", ("id", "ID", false, true), ("fileName", "文件名", true, true), ("contentType", "内容类型", true, true), ("size", "大小", true, true), ("relativePath", "相对路径", false, true), ("extension", "扩展名", true, true), ("previewSupported", "可预览", true, true), ("previewCategory", "预览分类", true, true), ("previewType", "预览方式", true, true), ("createdTime", "上传时间", true, true), ("remark", "备注", true, true)), cancellationToken);
        await UpsertDefaultDefinitionAsync("system_dict_type", "字典类型视图", "system:dict", "list", 5000, DefaultDesignJson("system_dict_type", "字典类型视图", "system:dict", "list", ("id", "ID", false, true), ("dictName", "字典名称", true, true), ("dictCode", "字典编码", true, true), ("isEnabled", "启用", true, true), ("createdTime", "创建时间", false, true), ("remark", "备注", false, false)), cancellationToken);
        await UpsertDefaultDefinitionAsync("system_dict_item", "字典项视图", "system:dict", "list", 5000, DefaultDesignJson("system_dict_item", "字典项视图", "system:dict", "list", ("id", "ID", false, true), ("dictTypeId", "字典类型ID", false, true), ("dictName", "字典名称", false, true), ("dictCode", "字典编码", false, true), ("itemLabel", "字典标签", true, true), ("itemValue", "字典值", true, true), ("sortOrder", "排序", true, true), ("isEnabled", "启用", true, true), ("createdTime", "创建时间", false, true), ("remark", "备注", false, false)), cancellationToken);
    }

    private async Task UpsertDefaultDefinitionAsync(
        string viewCode,
        string viewName,
        string menuCode,
        string viewType,
        int maxPageSize,
        string designJson,
        CancellationToken cancellationToken)
    {
        var entity = (await db.Queryable<SystemQueryViewDefinitionEntity>()
            .Where(item => item.ViewCode == viewCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        if (entity is null)
        {
            entity = new SystemQueryViewDefinitionEntity
            {
                ViewCode = viewCode,
                ViewName = viewName,
                ModuleCode = "system",
                MenuCode = menuCode,
                ViewType = viewType,
                IsDefault = true,
                IsEnabled = true,
                VersionNo = 1,
                DefaultPageSize = 20,
                MaxPageSize = maxPageSize,
                Status = "Published",
                DesignJson = designJson,
                Remark = "系统默认查询视图"
            };
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            entity.ViewName = viewName;
            entity.MenuCode = menuCode;
            entity.ViewType = viewType;
            entity.IsDefault = true;
            entity.IsEnabled = true;
            entity.VersionNo = Math.Max(1, entity.VersionNo);
            entity.DefaultPageSize = 20;
            entity.MaxPageSize = maxPageSize;
            entity.Status = "Published";
            entity.DesignJson = designJson;
            entity.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }

        await UpsertRuntimeAsync(entity, cancellationToken);
    }

    private async Task UpsertRuntimeAsync(SystemQueryViewDefinitionEntity definition, CancellationToken cancellationToken)
    {
        var runtime = (await db.Queryable<SystemQueryViewRuntimeEntity>()
            .Where(item => item.ViewId == definition.Id && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        if (runtime is null)
        {
            runtime = new SystemQueryViewRuntimeEntity
            {
                ViewId = definition.Id,
                StableViewName = definition.ViewCode,
                CurrentVersionViewName = $"{definition.ViewCode}:v{definition.VersionNo}",
                CurrentVersionNo = definition.VersionNo,
                HealthStatus = "healthy",
                LastCheckTime = DateTime.UtcNow
            };
            await db.Insertable(runtime).ExecuteCommandAsync(cancellationToken);
            return;
        }

        runtime.StableViewName = definition.ViewCode;
        runtime.CurrentVersionViewName = $"{definition.ViewCode}:v{definition.VersionNo}";
        runtime.CurrentVersionNo = definition.VersionNo;
        runtime.HealthStatus = "healthy";
        runtime.LastCheckTime = DateTime.UtcNow;
        runtime.LastError = null;
        runtime.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(runtime).ExecuteCommandAsync(cancellationToken);
    }

    private static string NormalizeSystemUserDesignJson(string designJson)
    {
        var root = JsonNode.Parse(designJson)?.AsObject();
        var projections = root?["projections"]?.AsArray();
        if (root is null || projections is null)
        {
            return designJson;
        }

        foreach (var projection in projections.OfType<JsonObject>())
        {
            var fieldCode = projection["fieldCode"]?.GetValue<string>();
            if (fieldCode is "deptName" or "positionName" or "roleNames")
            {
                projection["isQueryable"] = false;
                projection["isSortable"] = false;
            }
        }

        return root.ToJsonString();
    }

    private static string DefaultDesignJson(
        string viewCode,
        string viewName,
        string menuCode,
        string viewType,
        params (string Field, string Title, bool Visible, bool Queryable)[] fields)
    {
        var projections = string.Join(",", fields.Select(field =>
        {
            var projectionKey = $"{viewCode}:{field.Field}";
            var isQueryable = field.Queryable && !NonQueryableProjectionKeys.Contains(projectionKey);
            return $$"""{"columnResourceId":"","fieldCode":"{{field.Field}}","displayName":"{{field.Title}}","fieldAlias":"{{field.Field}}","dataType":"string","width":{{(field.Visible ? 140 : 120)}},"align":"left","isVisible":{{field.Visible.ToString().ToLowerInvariant()}},"isQueryable":{{isQueryable.ToString().ToLowerInvariant()}},"isSortable":{{isQueryable.ToString().ToLowerInvariant()}},"isExportable":{{field.Visible.ToString().ToLowerInvariant()}},"isFrozen":false,"dictType":null,"maskRule":null,"permissionCode":null}""";
        }));
        return $$"""
{"viewName":"{{viewName}}","viewCode":"{{viewCode}}","moduleCode":"system","menuCode":"{{menuCode}}","viewType":"{{viewType}}","isDefault":true,"isEnabled":true,"defaultPageSize":20,"maxPageSize":5000,"remark":"系统默认查询视图","tables":[],"relations":[],"projections":[{{projections}}],"conditions":[],"sorts":[{"fieldCode":"createdTime","direction":"desc","sortOrder":1}]}
""";
    }
}
