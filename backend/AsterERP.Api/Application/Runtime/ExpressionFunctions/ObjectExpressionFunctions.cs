using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.Runtime.ExpressionFunctions;

internal static class ObjectExpressionFunctions
{
    private const string ModuleKey = "object";
    private const string ModuleName = "对象";
    private const string NamespaceName = "ObjectFns";

    public static IReadOnlyList<RuntimeExpressionFunctionDefinitionDto> List() =>
    [
        Create("keys", "objectkeys", "键列表", "读取对象键", "array", []),
        Create("values", "objectvalues", "值列表", "读取对象值", "array", []),
        Create("get", "objectpath", "路径取值", "按路径读取对象值", "object", [P("path", "路径", "string")]),
        Create("has", "haspath", "路径存在", "判断路径是否存在", "boolean", [P("path", "路径", "string")]),
        Create("pick", "pickfields", "选择字段", "选择对象字段", "object", [P("fields", "字段列表", "string")]),
        Create("omit", "omitfields", "排除字段", "排除对象字段", "object", [P("fields", "字段列表", "string")]),
        Create("clone", "toobject", "对象克隆", "转换为普通对象", "object", []),
        Create("merge", "toobject", "对象合并", "保留为对象；复杂合并请用结构化表达式", "object", [])
    ];

    private static RuntimeExpressionFunctionDefinitionDto Create(
        string functionName,
        string canonicalName,
        string label,
        string description,
        string returnType,
        IReadOnlyList<RuntimeExpressionFunctionParameterDto> parameters) =>
        RuntimeExpressionFunctionDefinitionFactory.Create(ModuleKey, ModuleName, NamespaceName, functionName, canonicalName, label, description, returnType, parameters);

    private static RuntimeExpressionFunctionParameterDto P(string name, string label, string dataType) =>
        RuntimeExpressionFunctionDefinitionFactory.Parameter(name, label, dataType);
}
