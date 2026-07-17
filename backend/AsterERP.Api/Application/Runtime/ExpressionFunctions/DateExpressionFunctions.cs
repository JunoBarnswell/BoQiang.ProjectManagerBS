using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.Runtime.ExpressionFunctions;

internal static class DateExpressionFunctions
{
    private const string ModuleKey = "date";
    private const string ModuleName = "日期";
    private const string NamespaceName = "DateFns";

    public static IReadOnlyList<RuntimeExpressionFunctionDefinitionDto> List() =>
    [
        Create("now", "now", "当前时间", "返回当前 UTC 时间", "datetime", [], requiresInput: false, deterministic: false),
        Create("today", "today", "今天", "返回今天日期", "date", [], requiresInput: false, deterministic: false),
        Create("parseDate", "parsedate", "解析日期", "解析日期文本", "datetime", []),
        Create("formatDate", "formatdate", "格式化日期", "按格式输出日期", "string", [P("format", "格式", "string", required: false, defaultValue: "yyyy-MM-dd")]),
        Create("addDays", "adddays", "加天数", "日期加天数", "datetime", [P("days", "天数", "number")]),
        Create("addMonths", "addmonths", "加月份", "日期加月份", "datetime", [P("months", "月份", "number")]),
        Create("addYears", "addyears", "加年份", "日期加年份", "datetime", [P("years", "年份", "number")]),
        Create("diffDays", "diffdays", "相差天数", "计算两个日期相差天数", "number", [P("value", "比较日期", "datetime")]),
        Create("diffHours", "diffhours", "相差小时", "计算两个日期相差小时", "number", [P("value", "比较日期", "datetime")]),
        Create("startOfDay", "startofday", "当天开始", "返回当天开始时间", "datetime", []),
        Create("endOfDay", "endofday", "当天结束", "返回当天结束时间", "datetime", []),
        Create("dateToTimestamp", "timestamp", "日期转时间戳", "日期转毫秒时间戳", "number", []),
        Create("timestampToDate", "fromtimestamp", "时间戳转日期", "时间戳转日期", "datetime", []),
        Create("getYear", "year", "年", "读取年份", "number", []),
        Create("getMonth", "month", "月", "读取月份", "number", []),
        Create("getDay", "day", "日", "读取日期中的日", "number", [])
    ];

    private static RuntimeExpressionFunctionDefinitionDto Create(
        string functionName,
        string canonicalName,
        string label,
        string description,
        string returnType,
        IReadOnlyList<RuntimeExpressionFunctionParameterDto> parameters,
        bool requiresInput = true,
        bool deterministic = true) =>
        RuntimeExpressionFunctionDefinitionFactory.Create(ModuleKey, ModuleName, NamespaceName, functionName, canonicalName, label, description, returnType, parameters, requiresInput: requiresInput, deterministic: deterministic);

    private static RuntimeExpressionFunctionParameterDto P(string name, string label, string dataType, bool required = true, object? defaultValue = null) =>
        RuntimeExpressionFunctionDefinitionFactory.Parameter(name, label, dataType, required: required, defaultValue: defaultValue);
}
