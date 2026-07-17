using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.Runtime.ExpressionFunctions;

internal static class StringExpressionFunctions
{
    private const string ModuleKey = "string";
    private const string ModuleName = "字符串";
    private const string NamespaceName = "StringFns";

    public static IReadOnlyList<RuntimeExpressionFunctionDefinitionDto> List() =>
    [
        Create("isEmpty", "isempty", "是否为空", "判断 null、undefined 或空白字符串", "boolean", []),
        Create("isNotEmpty", "isnotempty", "是否非空", "判断值是否非空", "boolean", []),
        Create("isBlank", "isempty", "是否空白", "判断字符串是否为空白", "boolean", []),
        Create("isNotBlank", "isnotempty", "是否非空白", "判断字符串是否非空白", "boolean", []),
        Create("equals", "equals", "相等", "忽略大小写比较文本", "boolean", [P("value", "比较值", "string")]),
        Create("equalsIgnoreCase", "equals", "忽略大小写相等", "忽略大小写比较文本", "boolean", [P("value", "比较值", "string")]),
        Create("length", "length", "长度", "获取字符串长度", "number", []),
        Create("substring", "substring", "截取", "从起始位置截取指定长度", "string", [P("start", "起始", "number"), P("length", "长度", "number", required: false)]),
        Create("slice", "substring", "切片", "按起始位置和长度截取", "string", [P("start", "起始", "number"), P("length", "长度", "number", required: false)]),
        Create("left", "left", "左侧截取", "取左侧 N 个字符", "string", [P("length", "长度", "number")]),
        Create("right", "right", "右侧截取", "取右侧 N 个字符", "string", [P("length", "长度", "number")]),
        Create("mid", "substring", "中间截取", "从起始位置取指定长度", "string", [P("start", "起始", "number"), P("length", "长度", "number")]),
        Create("truncate", "truncate", "截断", "超长截断并追加后缀", "string", [P("length", "长度", "number"), P("suffix", "后缀", "string", required: false, defaultValue: "...")]),
        Create("truncateWithEllipsis", "truncate", "省略截断", "超长截断并追加省略号", "string", [P("length", "长度", "number")]),
        Create("contains", "contains", "包含", "判断是否包含关键字", "boolean", [P("value", "关键字", "string")]),
        Create("startsWith", "startswith", "开头匹配", "判断是否以指定内容开头", "boolean", [P("value", "前缀", "string")]),
        Create("endsWith", "endswith", "结尾匹配", "判断是否以指定内容结尾", "boolean", [P("value", "后缀", "string")]),
        Create("replace", "replace", "替换", "替换文本", "string", [P("oldValue", "旧值", "string"), P("newValue", "新值", "string")]),
        Create("trim", "trim", "去两端空格", "去除两端空白", "string", [], examples: ["StringFns.trim(@keyword)"]),
        Create("trimStart", "ltrim", "去开头空格", "去除开头空白", "string", []),
        Create("trimEnd", "rtrim", "去结尾空格", "去除结尾空白", "string", []),
        Create("removeSpaces", "removespaces", "移除空格", "移除所有空白字符", "string", []),
        Create("removeLineBreaks", "removelinebreaks", "移除换行", "移除换行符", "string", []),
        Create("normalizeWhitespace", "normalizewhitespace", "合并空白", "多空白合并为单空格", "string", []),
        Create("removeSpecialChars", "striphtml", "移除特殊字符", "移除 HTML 标签和特殊片段", "string", []),
        Create("keepNumbers", "onlydigits", "只保留数字", "移除非数字字符", "string", []),
        Create("keepLetters", "onlyletters", "只保留字母", "移除非英文字母字符", "string", []),
        Create("keepChinese", "onlychinese", "只保留中文", "移除非中文字符", "string", []),
        Create("toUpper", "upper", "转大写", "转换为大写", "string", []),
        Create("toLower", "lower", "转小写", "转换为小写", "string", []),
        Create("capitalize", "capitalize", "首字母大写", "首字母大写其余小写", "string", []),
        Create("camelCase", "camelcase", "小驼峰", "转换为小驼峰命名", "string", []),
        Create("snakeCase", "snakecase", "下划线", "转换为下划线命名", "string", []),
        Create("kebabCase", "kebabcase", "中划线", "转换为中划线命名", "string", []),
        Create("join", "join", "数组拼接", "将数组拼接为字符串", "string", [P("separator", "分隔符", "string", required: false, defaultValue: ",")]),
        Create("padStart", "padleft", "左侧补位", "左侧填充到指定长度", "string", [P("length", "长度", "number"), P("char", "填充字符", "string", required: false, defaultValue: " ")]),
        Create("padEnd", "padright", "右侧补位", "右侧填充到指定长度", "string", [P("length", "长度", "number"), P("char", "填充字符", "string", required: false, defaultValue: " ")]),
        Create("repeat", "repeat", "重复", "重复字符串", "string", [P("count", "次数", "number")]),
        Create("encodeUrl", "encodeuri", "URL 编码", "URL 编码", "string", []),
        Create("decodeUrl", "decodeuri", "URL 解码", "URL 解码", "string", []),
        Create("base64Encode", "base64encode", "Base64 编码", "UTF-8 Base64 编码", "string", []),
        Create("base64Decode", "base64decode", "Base64 解码", "UTF-8 Base64 解码", "string", [])
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
