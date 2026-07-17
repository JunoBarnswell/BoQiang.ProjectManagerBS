using AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Application.Runtime.ExpressionFunctions;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.Runtime;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class RuntimeExpressionFunctionCatalogTests
{
    private static readonly string[] RequiredNamespaces =
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

    [Fact]
    public void GetCatalog_MicroflowSqlScript_ReturnsCompleteNamespacedCatalog()
    {
        var catalog = new RuntimeExpressionFunctionCatalog();
        var response = catalog.GetCatalog("microflowSqlScript");

        Assert.Equal("microflowSqlScript", response.Scope);
        Assert.All(RequiredNamespaces, ns =>
            Assert.Contains(response.Functions, item => item.Namespace == ns));
        Assert.All(response.Functions, function =>
        {
            Assert.NotEmpty(function.ModuleKey);
            Assert.NotEmpty(function.ModuleName);
            Assert.NotEmpty(function.Namespace);
            Assert.NotEmpty(function.FunctionName);
            Assert.NotEmpty(function.QualifiedName);
            Assert.NotEmpty(function.CanonicalName);
            Assert.NotEmpty(function.ReturnType);
            Assert.True(function.SqlEnabled);
            Assert.StartsWith(function.Namespace + ".", function.QualifiedName, StringComparison.Ordinal);
            Assert.Same(
                catalog.Resolve(function.QualifiedName, requireNamespace: true, requireSqlEnabled: true),
                catalog.Resolve(function.QualifiedName, requireNamespace: true, requireSqlEnabled: true));
        });
        Assert.Equal(
            response.Functions.Count,
            response.Functions.Select(item => item.QualifiedName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Evaluate_AllSqlEnabledCatalogFunctions_CanExecuteThroughSqlExpressionEvaluator()
    {
        var catalog = new RuntimeExpressionFunctionCatalog();
        var evaluator = CreateEvaluator();
        var variables = CreateVariables();
        var failures = new List<string>();

        foreach (var definition in catalog.GetCatalog("microflowSqlScript").Functions)
        {
            var expression = BuildExpression(definition);
            try
            {
                _ = evaluator.Evaluate(expression, variables);
            }
            catch (Exception exception)
            {
                failures.Add($"{definition.QualifiedName}: {expression} => {exception.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static ApplicationDataCenterSqlScriptExpressionEvaluator CreateEvaluator()
    {
        var tokenizer = new ApplicationDataCenterSqlScriptExpressionTokenizer();
        var parser = new ApplicationDataCenterSqlScriptExpressionParser(tokenizer);
        return new ApplicationDataCenterSqlScriptExpressionEvaluator(
            parser,
            new RuntimeExpressionFunctionCatalog(),
            new RuntimeExpressionHelperCatalog(),
            new ApplicationDataCenterSqlRbacFunctionEvaluator(CreateCurrentUser()));
    }

    private static Dictionary<string, object?> CreateVariables() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["array"] = new object?[]
            {
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["id"] = "1",
                    ["name"] = "Alpha",
                    ["category"] = "A",
                    ["amount"] = 10
                },
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["id"] = "2",
                    ["name"] = "Beta",
                    ["category"] = "B",
                    ["amount"] = 20
                }
            },
            ["base64"] = "SGVsbG8=",
            ["bool"] = true,
            ["date"] = "2024-01-02T03:04:05Z",
            ["email"] = "demo@example.com",
            ["guid"] = "00000000-0000-0000-0000-000000000000",
            ["idCard"] = "110101199001011234",
            ["jsonText"] = "{\"nested\":{\"name\":\"Alpha\"},\"items\":[1,2]}",
            ["name"] = "Alice",
            ["nestedArray"] = new object?[] { new object?[] { 1, 2 }, new object?[] { 3 } },
            ["number"] = 12.75m,
            ["object"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = "1",
                ["name"] = "Alpha",
                ["nested"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["name"] = "Nested"
                }
            },
            ["otherDate"] = "2024-01-04T03:04:05Z",
            ["phone"] = "13812345678",
            ["text"] = "  abc def  ",
            ["timestamp"] = 1704164645000L,
            ["url"] = "https://example.com/a?x=1",
            ["urlEncoded"] = "https%3A%2F%2Fexample.com%2Fa%3Fx%3D1"
        };

    private static string BuildExpression(RuntimeExpressionFunctionDefinitionDto definition)
    {
        var args = new List<string>();
        if (definition.RequiresInput)
        {
            args.Add(InputFor(definition));
        }

        args.AddRange(definition.Parameters.Select(parameter => ArgumentFor(definition, parameter)));
        return $"{definition.QualifiedName}({string.Join(", ", args)})";
    }

    private static string InputFor(RuntimeExpressionFunctionDefinitionDto definition)
    {
        var qualifiedName = definition.QualifiedName;
        return qualifiedName switch
        {
            "ArrayFns.flatten" => "@nestedArray",
            "DateFns.timestampToDate" => "@timestamp",
            "FormatFns.booleanText" => "@bool",
            "FormatFns.currency" or "FormatFns.formatCurrency" or "FormatFns.percent" or "FormatFns.formatPercent" => "@number",
            "FormatFns.formatDate" => "@date",
            "FormatFns.maskBankCard" => "'6222021234567890123'",
            "FormatFns.maskEmail" => "@email",
            "FormatFns.maskIdCard" => "@idCard",
            "FormatFns.maskName" => "@name",
            "FormatFns.maskPhone" => "@phone",
            "FormatFns.mapValue" => "'A'",
            "JsonFns.flatten" => "@nestedArray",
            "JsonFns.isJson" or "JsonFns.jsonParse" or "JsonFns.parse" => "@jsonText",
            "JsonFns.jsonStringify" or "JsonFns.stringify" => "@object",
            "RegexFns.isEmail" => "@email",
            "RegexFns.isPhone" => "@phone",
            "RegexFns.isUrl" => "@url",
            "RegexFns.isUuid" => "@guid",
            "RegexFns.isChinese" => "'中文'",
            "RegexFns.isNumberString" => "'12345'",
            "StringFns.base64Decode" => "@base64",
            "StringFns.join" => "@array",
            "TypeFns.isArray" or "TypeFns.toArray" => "@array",
            "TypeFns.isBoolean" or "TypeFns.toBoolean" => "@bool",
            "TypeFns.isDate" => "@date",
            "TypeFns.isNil" or "TypeFns.isNull" or "TypeFns.isUndefined" => "null",
            "TypeFns.isNumber" or "TypeFns.toNumber" => "@number",
            "TypeFns.isObject" or "TypeFns.toObject" => "@object",
            "UrlFns.decodeUrl" or "UrlFns.decodeUrlComponent" => "@urlEncoded",
            "UrlFns.isUrl" => "@url",
            _ when definition.Namespace is "ArrayFns" => "@array",
            _ when definition.Namespace is "DateFns" => "@date",
            _ when definition.Namespace is "JsonFns" or "ObjectFns" => "@object",
            _ when definition.Namespace is "NumberFns" => "@number",
            _ when definition.Namespace is "UrlFns" => "@url",
            _ => "@text"
        };
    }

    private static string ArgumentFor(
        RuntimeExpressionFunctionDefinitionDto definition,
        RuntimeExpressionFunctionParameterDto parameter)
    {
        return parameter.Name switch
        {
            "char" => "'0'",
            "count" => "2",
            "days" => "1",
            "defaultValue" => "'Other'",
            "digits" => "2",
            "direction" => "'desc'",
            "end" => "2",
            "field" => "'amount'",
            "fields" => "'id,name'",
            "falseText" => "'no'",
            "format" => "'yyyy-MM-dd'",
            "index" => "0",
            "length" => "2",
            "mapping" => "'A=Alpha;B=Beta'",
            "mask" => "'*'",
            "max" => "20",
            "min" => "1",
            "months" => "1",
            "newValue" => "'z'",
            "oldValue" => "'a'",
            "path" => "'nested.name'",
            "separator" => "'|'",
            "start" => "1",
            "suffix" => "'...'",
            "symbol" => "'$'",
            "trueText" => "'yes'",
            "value" when definition.Namespace == "DateFns" => "@otherDate",
            "value" when definition.Namespace == "NumberFns" => "3",
            "value" when definition.Namespace == "ArrayFns" => "'A'",
            "value" => "'a'",
            "years" => "1",
            _ => "'x'"
        };
    }

    private static ICurrentUser CreateCurrentUser()
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            "admin",
            "admin",
            "tenant-a",
            "客户A",
            "MES",
            "客户A MES",
            "dept-a",
            "position-a",
            ["role-id-admin"],
            ["MES_ADMIN"],
            ["*"],
            "ALL",
            true,
            true,
            true,
            "平台管理员"));
        return new FixedAsterErpCurrentUser(principal);
    }
}
