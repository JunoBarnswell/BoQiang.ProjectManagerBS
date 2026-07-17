using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.Runtime.ExpressionFunctions;

internal static class FormatExpressionFunctions
{
    private const string ModuleKey = "format";
    private const string ModuleName = "格式化";
    private const string NamespaceName = "FormatFns";

    public static IReadOnlyList<RuntimeExpressionFunctionDefinitionDto> List() =>
    [
        Create("currency", "currency", "金额", "金额格式化", "string", [P("symbol", "货币符号", "string", required: false, defaultValue: "¥")]),
        Create("formatCurrency", "currency", "金额", "金额格式化", "string", [P("symbol", "货币符号", "string", required: false, defaultValue: "¥")]),
        Create("percent", "percent", "百分比", "百分比格式化", "string", [P("digits", "小数位", "number", required: false, defaultValue: 2)]),
        Create("formatPercent", "percent", "百分比", "百分比格式化", "string", [P("digits", "小数位", "number", required: false, defaultValue: 2)]),
        Create("formatDate", "formatdate", "日期", "日期格式化", "string", [P("format", "格式", "string", required: false, defaultValue: "yyyy-MM-dd")]),
        Create("maskPhone", "maskphone", "手机号脱敏", "手机号脱敏", "string", []),
        Create("maskEmail", "maskemail", "邮箱脱敏", "邮箱脱敏", "string", []),
        Create("maskIdCard", "maskidcard", "身份证脱敏", "身份证脱敏", "string", []),
        Create("maskBankCard", "maskbankcard", "银行卡脱敏", "银行卡脱敏", "string", []),
        Create("maskName", "maskname", "姓名脱敏", "姓名脱敏", "string", []),
        Create("maskCustom", "masktext", "自定义脱敏", "自定义脱敏", "string", [P("start", "保留开头", "number"), P("end", "保留结尾", "number"), P("mask", "掩码", "string", required: false, defaultValue: "***")]),
        Create("booleanText", "booleantext", "布尔文本", "布尔值映射文本", "string", [P("trueText", "真文本", "string"), P("falseText", "假文本", "string")]),
        Create("mapValue", "mapvalue", "值映射", "按 key=value 映射文本", "string", [P("mapping", "映射", "string"), P("defaultValue", "默认值", "string", required: false)])
    ];

    private static RuntimeExpressionFunctionDefinitionDto Create(
        string functionName,
        string canonicalName,
        string label,
        string description,
        string returnType,
        IReadOnlyList<RuntimeExpressionFunctionParameterDto> parameters) =>
        RuntimeExpressionFunctionDefinitionFactory.Create(ModuleKey, ModuleName, NamespaceName, functionName, canonicalName, label, description, returnType, parameters);

    private static RuntimeExpressionFunctionParameterDto P(string name, string label, string dataType, bool required = true, object? defaultValue = null) =>
        RuntimeExpressionFunctionDefinitionFactory.Parameter(name, label, dataType, required: required, defaultValue: defaultValue);
}
