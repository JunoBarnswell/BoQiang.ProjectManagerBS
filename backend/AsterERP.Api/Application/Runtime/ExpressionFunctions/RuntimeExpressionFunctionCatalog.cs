using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Runtime.ExpressionFunctions;

public sealed class RuntimeExpressionFunctionCatalog
{
    private static readonly string[] Namespaces =
    [
        "StringFns",
        "NumberFns",
        "DateFns",
        "FormatFns",
        "JsonFns",
        "ObjectFns",
        "ArrayFns",
        "RegexFns",
        "UrlFns",
        "TypeFns",
        "RbacFns"
    ];

    private readonly IReadOnlyList<RuntimeExpressionFunctionDefinitionDto> definitions;
    private readonly Dictionary<string, RuntimeExpressionFunctionDefinitionDto> byQualifiedName;
    private readonly Dictionary<string, RuntimeExpressionFunctionDefinitionDto> byCanonicalName;

    public RuntimeExpressionFunctionCatalog()
    {
        definitions =
        [
            .. StringExpressionFunctions.List(),
            .. NumberExpressionFunctions.List(),
            .. DateExpressionFunctions.List(),
            .. FormatExpressionFunctions.List(),
            .. JsonExpressionFunctions.List(),
            .. ObjectExpressionFunctions.List(),
            .. ArrayExpressionFunctions.List(),
            .. RegexExpressionFunctions.List(),
            .. UrlExpressionFunctions.List(),
            .. TypeExpressionFunctions.List(),
            .. RbacExpressionFunctions.List()
        ];
        byQualifiedName = definitions
            .GroupBy(item => NormalizeLookupKey(item.QualifiedName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        byCanonicalName = definitions
            .GroupBy(item => NormalizeLookupKey(item.CanonicalName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public RuntimeExpressionFunctionCatalogResponse GetCatalog(string? scope) =>
        new()
        {
            Functions = definitions
                .Where(item => IsMicroflowSqlScriptScope(scope) ? item.SqlEnabled : !IsSqlScriptOnlyFunction(item))
                .OrderBy(item => item.ModuleKey, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.FunctionName, StringComparer.OrdinalIgnoreCase)
                .Select(Clone)
                .ToList(),
            Scope = string.IsNullOrWhiteSpace(scope) ? "all" : scope.Trim()
        };

    public bool Supports(string functionName) =>
        TryResolve(functionName, requireNamespace: false, requireSqlEnabled: false, out _);

    public RuntimeExpressionFunctionDefinitionDto Resolve(
        string functionName,
        bool requireNamespace,
        bool requireSqlEnabled)
    {
        if (TryResolve(functionName, requireNamespace, requireSqlEnabled, out var definition))
        {
            return definition;
        }

        throw new ValidationException($"变量辅助函数不支持: {functionName}", ErrorCodes.ParameterInvalid);
    }

    public bool TryResolve(
        string functionName,
        bool requireNamespace,
        bool requireSqlEnabled,
        out RuntimeExpressionFunctionDefinitionDto definition)
    {
        definition = new RuntimeExpressionFunctionDefinitionDto();
        if (string.IsNullOrWhiteSpace(functionName))
        {
            return false;
        }

        var normalizedName = NormalizeLookupKey(functionName);
        var hasNamespace = normalizedName.Contains('.', StringComparison.Ordinal);
        if (requireNamespace && !hasNamespace)
        {
            return false;
        }

        var resolved = hasNamespace
            ? byQualifiedName.TryGetValue(normalizedName, out var qualifiedDefinition) ? qualifiedDefinition : null
            : byCanonicalName.TryGetValue(normalizedName, out var canonicalDefinition) ? canonicalDefinition : null;
        if (resolved is null)
        {
            return false;
        }

        if (requireSqlEnabled && !resolved.SqlEnabled)
        {
            return false;
        }

        definition = resolved;
        return true;
    }

    public static string NormalizeHelperName(string functionName)
    {
        var normalized = NormalizeLookupKey(functionName);
        if (!normalized.Contains('.', StringComparison.Ordinal))
        {
            return NormalizeAlias(normalized);
        }

        var functionPart = normalized[(normalized.LastIndexOf('.') + 1)..];
        return NormalizeAlias(functionPart);
    }

    public static bool LooksLikeNamespacedFunction(string value)
    {
        var normalized = value.Trim();
        return Namespaces.Any(item => normalized.StartsWith($"{item}.", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsKnownNamespace(string namespaceName) =>
        Namespaces.Any(item => string.Equals(item, namespaceName.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool LooksLikeRuntimeFunctionNamespace(string namespaceName) =>
        IsKnownNamespace(namespaceName) ||
        namespaceName.Trim().EndsWith("Fns", StringComparison.OrdinalIgnoreCase);

    private static bool IsMicroflowSqlScriptScope(string? scope) =>
        string.Equals(scope?.Trim(), "microflowSqlScript", StringComparison.OrdinalIgnoreCase);

    private static bool IsSqlScriptOnlyFunction(RuntimeExpressionFunctionDefinitionDto definition) =>
        string.Equals(definition.Namespace, RbacExpressionFunctions.NamespaceName, StringComparison.OrdinalIgnoreCase);

    private static RuntimeExpressionFunctionDefinitionDto Clone(RuntimeExpressionFunctionDefinitionDto source) =>
        new()
        {
            CanonicalName = source.CanonicalName,
            Description = source.Description,
            Deterministic = source.Deterministic,
            DisabledReason = source.DisabledReason,
            Examples = [.. source.Examples],
            FunctionName = source.FunctionName,
            Label = source.Label,
            ModuleKey = source.ModuleKey,
            ModuleName = source.ModuleName,
            Namespace = source.Namespace,
            Parameters = source.Parameters
                .Select(item => new RuntimeExpressionFunctionParameterDto
                {
                    DataType = item.DataType,
                    DefaultValue = item.DefaultValue,
                    Description = item.Description,
                    Label = item.Label,
                    Name = item.Name,
                    Required = item.Required
                })
                .ToList(),
            QualifiedName = source.QualifiedName,
            RequiresInput = source.RequiresInput,
            ReturnType = source.ReturnType,
            SqlEnabled = source.SqlEnabled
        };

    private static string NormalizeLookupKey(string value) =>
        value.Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

    private static string NormalizeAlias(string value) =>
        value switch
        {
            "toupper" => "upper",
            "tolower" => "lower",
            "trimstart" => "ltrim",
            "trimend" => "rtrim",
            "isblank" => "isempty",
            "isnotblank" => "isnotempty",
            "equalsignorecase" => "equals",
            "toint" => "tointeger",
            "tofloat" => "tonumber",
            "roundto" => "round",
            "formatcurrency" => "currency",
            "formatpercent" => "percent",
            "jsonparse" => "parsejson",
            "jsonstringify" => "stringifyjson",
            "getvalue" => "objectpath",
            "hasvalue" => "haspath",
            "maskcustom" => "masktext",
            "distinctby" => "uniqueby",
            "avg" => "average",
            "keepnumbers" => "onlydigits",
            "keepletters" => "onlyletters",
            "keepchinese" => "onlychinese",
            "encodeurl" => "encodeuri",
            "decodeurl" => "decodeuri",
            "encodeurlcomponent" => "encodeuri",
            "decodeurlcomponent" => "decodeuri",
            "timestamptodate" => "fromtimestamp",
            "datetotimestamp" => "timestamp",
            "getyear" => "year",
            "getmonth" => "month",
            "getday" => "day",
            "isundefined" => "isnull",
            "isnil" => "isnull",
            _ => value
        };
}
