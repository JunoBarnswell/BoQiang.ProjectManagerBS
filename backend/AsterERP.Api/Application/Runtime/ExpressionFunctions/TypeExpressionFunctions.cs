using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.Runtime.ExpressionFunctions;

internal static class TypeExpressionFunctions
{
    private const string ModuleKey = "type";
    private const string ModuleName = "类型判断";
    private const string NamespaceName = "TypeFns";

    public static IReadOnlyList<RuntimeExpressionFunctionDefinitionDto> List() =>
    [
        Create("toString", "tostring", "转字符串", "转换为字符串", "string", []),
        Create("toNumber", "tonumber", "转数字", "转换为数字", "number", []),
        Create("toBoolean", "toboolean", "转布尔", "转换为布尔", "boolean", []),
        Create("toArray", "toarray", "转数组", "转换为数组", "array", []),
        Create("toObject", "toobject", "转对象", "转换为对象", "object", []),
        Create("isString", "isstring", "是否字符串", "判断字符串", "boolean", []),
        Create("isNumber", "isnumber", "是否数字", "判断数字", "boolean", []),
        Create("isBoolean", "isboolean", "是否布尔", "判断布尔", "boolean", []),
        Create("isArray", "isarray", "是否数组", "判断数组", "boolean", []),
        Create("isObject", "isobject", "是否对象", "判断对象", "boolean", []),
        Create("isDate", "isdate", "是否日期", "判断日期", "boolean", []),
        Create("isNull", "isnull", "是否 null", "判断 null", "boolean", []),
        Create("isUndefined", "isnull", "是否 undefined", "后端 JSON 中按 null 判断", "boolean", []),
        Create("isNil", "isnull", "是否空值", "判断 null 或 undefined", "boolean", [])
    ];

    private static RuntimeExpressionFunctionDefinitionDto Create(
        string functionName,
        string canonicalName,
        string label,
        string description,
        string returnType,
        IReadOnlyList<RuntimeExpressionFunctionParameterDto> parameters) =>
        RuntimeExpressionFunctionDefinitionFactory.Create(ModuleKey, ModuleName, NamespaceName, functionName, canonicalName, label, description, returnType, parameters);
}
