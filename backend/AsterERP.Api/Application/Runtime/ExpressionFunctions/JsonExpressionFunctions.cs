using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.Runtime.ExpressionFunctions;

internal static class JsonExpressionFunctions
{
    private const string ModuleKey = "json";
    private const string ModuleName = "JSON";
    private const string NamespaceName = "JsonFns";

    public static IReadOnlyList<RuntimeExpressionFunctionDefinitionDto> List() =>
    [
        Create("parse", "parsejson", "解析 JSON", "JSON 文本转对象", "json", []),
        Create("jsonParse", "parsejson", "解析 JSON", "JSON 文本转对象", "json", []),
        Create("stringify", "stringifyjson", "转 JSON 文本", "对象转 JSON 文本", "string", []),
        Create("jsonStringify", "stringifyjson", "转 JSON 文本", "对象转 JSON 文本", "string", []),
        Create("isJson", "isjson", "是否 JSON", "判断是否有效 JSON", "boolean", []),
        Create("getValue", "objectpath", "路径取值", "按点路径读取对象值", "object", [P("path", "路径", "string")]),
        Create("hasValue", "haspath", "路径存在", "判断点路径是否存在", "boolean", [P("path", "路径", "string")]),
        Create("keys", "objectkeys", "键列表", "读取对象键", "array", []),
        Create("values", "objectvalues", "值列表", "读取对象值", "array", []),
        Create("pick", "pickfields", "选择字段", "选择对象字段", "object", [P("fields", "字段列表", "string")]),
        Create("omit", "omitfields", "排除字段", "排除对象字段", "object", [P("fields", "字段列表", "string")]),
        Create("flatten", "flatten", "数组扁平", "扁平化一层数组", "array", [])
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
