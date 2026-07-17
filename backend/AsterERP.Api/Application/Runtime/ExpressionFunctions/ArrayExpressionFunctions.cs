using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.Runtime.ExpressionFunctions;

internal static class ArrayExpressionFunctions
{
    private const string ModuleKey = "array";
    private const string ModuleName = "数组";
    private const string NamespaceName = "ArrayFns";

    public static IReadOnlyList<RuntimeExpressionFunctionDefinitionDto> List() =>
    [
        Create("length", "count", "数量", "获取数组数量", "number", []),
        Create("count", "count", "数量", "获取数组数量", "number", []),
        Create("first", "first", "第一项", "读取第一项", "object", []),
        Create("last", "last", "最后一项", "读取最后一项", "object", []),
        Create("nth", "nth", "第 N 项", "读取指定索引项", "object", [P("index", "索引", "number")]),
        Create("join", "join", "拼接", "拼接为字符串", "string", [P("separator", "分隔符", "string", required: false, defaultValue: ",")]),
        Create("distinct", "distinct", "去重", "数组去重", "array", []),
        Create("distinctBy", "uniqueby", "按字段去重", "按字段去重", "array", [P("field", "字段", "string")]),
        Create("mapField", "mapfield", "字段数组", "提取字段值数组", "array", [P("field", "字段", "string")]),
        Create("filterEquals", "filterequals", "字段等于过滤", "按字段等于过滤", "array", [P("field", "字段", "string"), P("value", "值", "object")]),
        Create("filterContains", "filtercontains", "字段包含过滤", "按字段包含过滤", "array", [P("field", "字段", "string"), P("value", "值", "object")]),
        Create("findByField", "findbyfield", "字段查找", "按字段等于查找第一项", "object", [P("field", "字段", "string"), P("value", "值", "object")]),
        Create("sortBy", "sortby", "字段排序", "按字段排序", "array", [P("field", "字段", "string"), P("direction", "方向", "string", required: false, defaultValue: "asc")]),
        Create("groupBy", "groupby", "字段分组", "按字段分组", "object", [P("field", "字段", "string")]),
        Create("sum", "sum", "求和", "按字段求和", "number", [P("field", "字段", "string", required: false)]),
        Create("avg", "average", "平均值", "按字段求平均值", "number", [P("field", "字段", "string", required: false)]),
        Create("max", "maxby", "最大值", "按字段取最大值", "number", [P("field", "字段", "string", required: false)]),
        Create("min", "minby", "最小值", "按字段取最小值", "number", [P("field", "字段", "string", required: false)]),
        Create("slice", "slice", "切片", "截取数组片段", "array", [P("start", "起始", "number"), P("count", "数量", "number")]),
        Create("take", "take", "取前 N 项", "取前 N 项", "array", [P("count", "数量", "number")]),
        Create("skip", "skip", "跳过 N 项", "跳过 N 项", "array", [P("count", "数量", "number")]),
        Create("reverse", "reverse", "反转", "反转数组", "array", []),
        Create("flatten", "flatten", "扁平", "扁平化一层数组", "array", [])
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
