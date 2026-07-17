using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.Runtime.ExpressionFunctions;

internal static class RbacExpressionFunctions
{
    private const string ModuleKey = "rbac";
    private const string ModuleName = "权限";

    public const string NamespaceName = "RbacFns";

    public static IReadOnlyList<RuntimeExpressionFunctionDefinitionDto> List() =>
    [
        Create(
            "hasPermission",
            "haspermission",
            "拥有权限",
            "判断当前用户是否拥有指定权限，SQL 中返回 1 或 0",
            [
                P("permissionCode", "权限编码", "string")
            ],
            ["RbacFns.hasPermission('mes:order:viewAll')"]),
        Create(
            "hasAnyPermission",
            "hasanypermission",
            "拥有任一权限",
            "判断当前用户是否拥有任一权限，SQL 中返回 1 或 0",
            [
                P("permissionCode1", "权限编码 1", "string"),
                P("permissionCode2", "权限编码 2", "string", required: false),
                P("permissionCode3", "权限编码 3", "string", required: false),
                P("permissionCode4", "权限编码 4", "string", required: false),
                P("permissionCode5", "权限编码 5", "string", required: false)
            ],
            ["RbacFns.hasAnyPermission('mes:order:viewAll', 'mes:order:admin')"]),
        Create(
            "hasRole",
            "hasrole",
            "拥有角色",
            "判断当前用户是否拥有指定角色，SQL 中返回 1 或 0",
            [
                P("roleCode", "角色编码", "string")
            ],
            ["RbacFns.hasRole('MES_ADMIN')"]),
        Create(
            "hasAnyRole",
            "hasanyrole",
            "拥有任一角色",
            "判断当前用户是否拥有任一角色，SQL 中返回 1 或 0",
            [
                P("roleCode1", "角色编码 1", "string"),
                P("roleCode2", "角色编码 2", "string", required: false),
                P("roleCode3", "角色编码 3", "string", required: false),
                P("roleCode4", "角色编码 4", "string", required: false),
                P("roleCode5", "角色编码 5", "string", required: false)
            ],
            ["RbacFns.hasAnyRole('MES_ADMIN', 'TENANT_ADMIN')"]),
        Create(
            "inDept",
            "indept",
            "属于部门",
            "判断当前用户是否存在指定部门任职，SQL 中返回 1 或 0",
            [
                P("deptId", "部门 ID", "string")
            ],
            ["RbacFns.inDept('finance')"]),
        Create(
            "inPosition",
            "inposition",
            "属于岗位",
            "判断当前用户是否存在指定岗位任职，SQL 中返回 1 或 0",
            [
                P("positionId", "岗位 ID", "string")
            ],
            ["RbacFns.inPosition('finance-manager')"])
    ];

    private static RuntimeExpressionFunctionDefinitionDto Create(
        string functionName,
        string canonicalName,
        string label,
        string description,
        IReadOnlyList<RuntimeExpressionFunctionParameterDto> parameters,
        IReadOnlyList<string> examples) =>
        RuntimeExpressionFunctionDefinitionFactory.Create(
            ModuleKey,
            ModuleName,
            NamespaceName,
            functionName,
            canonicalName,
            label,
            description,
            "number",
            parameters,
            requiresInput: false,
            sqlEnabled: true,
            deterministic: false,
            examples: examples);

    private static RuntimeExpressionFunctionParameterDto P(
        string name,
        string label,
        string dataType,
        bool required = true) =>
        RuntimeExpressionFunctionDefinitionFactory.Parameter(name, label, dataType, required: required);
}
