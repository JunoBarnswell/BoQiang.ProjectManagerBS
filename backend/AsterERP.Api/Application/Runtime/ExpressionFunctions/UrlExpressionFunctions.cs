using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.Runtime.ExpressionFunctions;

internal static class UrlExpressionFunctions
{
    private const string ModuleKey = "url";
    private const string ModuleName = "URL";
    private const string NamespaceName = "UrlFns";

    public static IReadOnlyList<RuntimeExpressionFunctionDefinitionDto> List() =>
    [
        Create("encodeUrl", "encodeuri", "URL 编码", "URL 编码", "string", []),
        Create("decodeUrl", "decodeuri", "URL 解码", "URL 解码", "string", []),
        Create("encodeUrlComponent", "encodeuri", "URL 参数编码", "URL 参数编码", "string", []),
        Create("decodeUrlComponent", "decodeuri", "URL 参数解码", "URL 参数解码", "string", []),
        Create("isUrl", "isurl", "是否 URL", "判断 URL", "boolean", [])
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
