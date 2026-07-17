using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.Runtime.ExpressionFunctions;

internal static class NumberExpressionFunctions
{
    private const string ModuleKey = "number";
    private const string ModuleName = "数值";
    private const string NamespaceName = "NumberFns";

    public static IReadOnlyList<RuntimeExpressionFunctionDefinitionDto> List() =>
    [
        Create("toNumber", "tonumber", "转数字", "转换为数字", "number", []),
        Create("toInt", "tointeger", "转整数", "转换为整数", "number", []),
        Create("toFloat", "tonumber", "转浮点数", "转换为数字", "number", []),
        Create("toString", "tostring", "转字符串", "转换为字符串", "string", []),
        Create("isNumber", "isnumber", "是否数字", "判断是否为数字", "boolean", []),
        Create("isInteger", "isinteger", "是否整数", "判断是否为整数", "boolean", []),
        Create("round", "round", "四舍五入", "按位数四舍五入", "number", [P("digits", "位数", "number", required: false, defaultValue: 0)]),
        Create("roundTo", "round", "保留小数", "保留 N 位小数并返回数字", "number", [P("digits", "位数", "number")], examples: ["NumberFns.roundTo(@amount, 2)"]),
        Create("floor", "floor", "向下取整", "向下取整", "number", []),
        Create("ceil", "ceil", "向上取整", "向上取整", "number", []),
        Create("toFixed", "fixed", "固定小数", "固定小数位并返回文本", "string", [P("digits", "位数", "number", required: false, defaultValue: 2)]),
        Create("abs", "abs", "绝对值", "取绝对值", "number", []),
        Create("max", "max", "最大值", "返回两个值中较大值", "number", [P("value", "比较值", "number")]),
        Create("min", "min", "最小值", "返回两个值中较小值", "number", [P("value", "比较值", "number")]),
        Create("pow", "power", "幂运算", "计算幂", "number", [P("value", "指数", "number")]),
        Create("clamp", "clamp", "限制范围", "将数值限制在最小值和最大值之间", "number", [P("min", "最小值", "number"), P("max", "最大值", "number")], examples: ["NumberFns.clamp(NumberFns.toInt(@pageSize), 1, 200)"]),
        Create("decimalAdd", "add", "精确加法", "数字相加", "number", [P("value", "加数", "number")]),
        Create("decimalSub", "subtract", "精确减法", "数字相减", "number", [P("value", "减数", "number")]),
        Create("decimalMul", "multiply", "精确乘法", "数字相乘", "number", [P("value", "乘数", "number")]),
        Create("decimalDiv", "divide", "精确除法", "数字相除", "number", [P("value", "除数", "number")])
    ];

    private static RuntimeExpressionFunctionDefinitionDto Create(
        string functionName,
        string canonicalName,
        string label,
        string description,
        string returnType,
        IReadOnlyList<RuntimeExpressionFunctionParameterDto> parameters,
        IReadOnlyList<string>? examples = null) =>
        RuntimeExpressionFunctionDefinitionFactory.Create(ModuleKey, ModuleName, NamespaceName, functionName, canonicalName, label, description, returnType, parameters, examples: examples);

    private static RuntimeExpressionFunctionParameterDto P(string name, string label, string dataType, bool required = true, object? defaultValue = null) =>
        RuntimeExpressionFunctionDefinitionFactory.Parameter(name, label, dataType, required: required, defaultValue: defaultValue);
}
