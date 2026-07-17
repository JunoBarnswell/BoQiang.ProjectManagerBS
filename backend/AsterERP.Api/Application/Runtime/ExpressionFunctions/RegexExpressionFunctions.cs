using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.Runtime.ExpressionFunctions;

internal static class RegexExpressionFunctions
{
    private const string ModuleKey = "regex";
    private const string ModuleName = "正则";
    private const string NamespaceName = "RegexFns";

    public static IReadOnlyList<RuntimeExpressionFunctionDefinitionDto> List() =>
    [
        Create("isEmail", "isemail", "邮箱", "判断邮箱", "boolean", []),
        Create("isPhone", "isphone", "手机号", "判断手机号", "boolean", []),
        Create("isUrl", "isurl", "URL", "判断 URL", "boolean", []),
        Create("isNumberString", "isnumber", "数字文本", "判断数字文本", "boolean", []),
        Create("isChinese", "containschinese", "包含中文", "判断是否包含中文", "boolean", []),
        Create("isUuid", "isguid", "UUID", "判断 UUID/GUID", "boolean", [])
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
