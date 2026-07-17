using AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Application.Runtime.ExpressionFunctions;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared.Exceptions;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataCenterSqlScriptExpressionTests
{
    [Fact]
    public void Evaluate_SupportsNamespacedNestedFunctions()
    {
        var evaluator = CreateEvaluator();
        var value = evaluator.Evaluate(
            "NumberFns.clamp(NumberFns.toInt(@pageSize), 1, 200)",
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["pageSize"] = "500"
            });

        Assert.Equal(200m, value);
    }

    [Fact]
    public void Parameterize_RewritesSqlRuntimeFunctionsToInternalVariables()
    {
        var parameterizer = CreateParameterizer();
        var variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["keyword"] = "  SO-001  ",
            ["pageSize"] = "500"
        };

        var result = parameterizer.Parameterize(
            "SELECT * FROM order_header WHERE order_no LIKE '%' || StringFns.trim(@keyword) || '%' LIMIT NumberFns.clamp(NumberFns.toInt(@pageSize), 1, 200)",
            variables);

        Assert.DoesNotContain("StringFns.trim", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NumberFns.clamp", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@__fn_1", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@__fn_2", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("SO-001", result.GeneratedVariables["__fn_1"]);
        Assert.Equal(200m, result.GeneratedVariables["__fn_2"]);
        Assert.Equal("SO-001", variables["__fn_1"]);
        Assert.Equal(200m, variables["__fn_2"]);
    }

    [Fact]
    public void Parameterize_IgnoresRuntimeFunctionNamesInsideStringsAndComments()
    {
        var parameterizer = CreateParameterizer();
        var variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["keyword"] = "  SO-001  "
        };

        var result = parameterizer.Parameterize(
            "SELECT 'StringFns.trim(@keyword)' AS literal -- NumberFns.toInt(@pageSize)\n/* DateFns.now() */ FROM orders WHERE order_no = StringFns.trim(@keyword)",
            variables);

        Assert.Contains("'StringFns.trim(@keyword)'", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("-- NumberFns.toInt(@pageSize)", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/* DateFns.now() */", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Single(result.GeneratedVariables);
        Assert.Equal("SO-001", result.GeneratedVariables["__fn_1"]);
    }

    [Fact]
    public void Parameterize_RbacFunctionsToInternalSqlParameters()
    {
        var parameterizer = CreateParameterizer(CreateCurrentUser(["mes:order:viewAll"], ["MES_ADMIN"]));
        var variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["currentUserId"] = "admin"
        };

        var result = parameterizer.Parameterize(
            "RETURN SELECT * FROM orders WHERE (RbacFns.hasPermission('mes:order:viewAll') = 1 OR created_by = @currentUserId) AND RbacFns.hasRole('MES_ADMIN') = 1;",
            variables);

        Assert.DoesNotContain("RbacFns.hasPermission", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RbacFns.hasRole", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@__fn_1", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@__fn_2", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, result.GeneratedVariables["__fn_1"]);
        Assert.Equal(1, result.GeneratedVariables["__fn_2"]);
    }

    [Fact]
    public void ValidateFunctionCalls_RejectsRbacFunctionVariableArgument()
    {
        var parameterizer = CreateParameterizer(CreateCurrentUser(["mes:order:viewAll"], ["MES_ADMIN"]));

        var exception = Assert.Throws<ValidationException>(() =>
            parameterizer.ValidateFunctionCalls("RETURN SELECT * FROM orders WHERE RbacFns.hasPermission(@permissionCode) = 1;"));

        Assert.Contains("字符串字面量", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateFunctionCalls_RejectsUnsafeRbacCodeLiteral()
    {
        var parameterizer = CreateParameterizer(CreateCurrentUser(["*"], ["MES_ADMIN"]));

        var exception = Assert.Throws<ValidationException>(() =>
            parameterizer.ValidateFunctionCalls("RETURN SELECT * FROM orders WHERE RbacFns.hasPermission('x''); DROP TABLE users; --') = 1;"));

        Assert.Contains("非法权限或角色编码", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parser_SupportsLegacyReturnForms()
    {
        var parser = new ApplicationDataCenterSqlScriptParser();

        var selectPlan = parser.Parse("DECLARE @value number; SET @value = 1; RETURN SELECT @value AS value;");
        var variablePlan = parser.Parse("DECLARE @value text; SET @value = 'ok'; RETURN @value;");
        var jsonPlan = parser.Parse("DECLARE @value text; SET @value = 'ok'; RETURN JSON {\"value\": @value};");

        Assert.Equal("select", selectPlan.ReturnKind);
        Assert.Equal("variable", variablePlan.ReturnKind);
        Assert.Equal("json", jsonPlan.ReturnKind);
        Assert.Contains("value", selectPlan.DeclaredVariableNames);
        Assert.Equal("value", variablePlan.ReturnVariableName);
        Assert.Contains("@value", jsonPlan.ReturnJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validator_AcceptsLegacyControlStatementsWithWhitelistedFunctions()
    {
        var parser = new ApplicationDataCenterSqlScriptParser();
        var validator = new ApplicationDataCenterSqlScriptValidator(parser, CreateParameterizer());
        var errors = new List<string>();

        validator.ValidateSqlScript(
            new ApplicationMicroflowSqlScriptDefinition
            {
                Script = """
                    DECLARE @keyword text;
                    DECLARE @items array;
                    DECLARE @total number;
                    SET @keyword = StringFns.trim(@keyword);
                    SET @total = 0;
                    IF (StringFns.isNotBlank(@keyword)) {
                        SET @total = NumberFns.decimalAdd(@total, 1);
                    } ELSE {
                        SET @total = 0;
                    }
                    FOR @item IN @items {
                        SET @total = NumberFns.decimalAdd(@total, @item.amount);
                    }
                    RETURN JSON {"keyword": @keyword, "total": @total};
                    """
            },
            errors,
            "unit");

        Assert.Empty(errors);
    }

    [Fact]
    public void Validator_RejectsSqlBuiltInVariableOverrides()
    {
        var parser = new ApplicationDataCenterSqlScriptParser();
        var validator = new ApplicationDataCenterSqlScriptValidator(parser, CreateParameterizer());
        var errors = new List<string>();

        validator.ValidateSqlScript(
            new ApplicationMicroflowSqlScriptDefinition
            {
                LocalVariables =
                [
                    new ApplicationMicroflowSqlScriptLocalVariableDefinition
                    {
                        DataType = "string",
                        Initializer = new() { Kind = "literal", DataType = "string", Value = "spoof" },
                        Name = "currentUserId"
                    }
                ],
                Script = """
                    DECLARE @auditNow datetime;
                    SET @auditCreatedBy = 'spoof';
                    RETURN SELECT @currentUserId AS current_user_id;
                    """
            },
            errors,
            "unit");

        Assert.Contains(errors, item => item.Contains("@currentUserId 是系统内置变量", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, item => item.Contains("不能声明内置变量 @auditNow", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, item => item.Contains("不能给内置变量 @auditCreatedBy 赋值", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_SupportsBinaryArithmeticComparisonAndVariablePaths()
    {
        var evaluator = CreateEvaluator();
        var variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["left"] = 3,
            ["right"] = 2,
            ["row"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["amount"] = 5
            }
        };

        Assert.Equal(5m, evaluator.Evaluate("@left + @right", variables));
        Assert.True((bool)evaluator.Evaluate("@row.amount >= 5", variables)!);
        Assert.Equal("A-3", evaluator.Evaluate("'A-' + @left", variables));
    }

    [Fact]
    public void ValidateFunctionCalls_RejectsBareHelperFunctionsInSetExpression()
    {
        var parameterizer = CreateParameterizer();

        Assert.Throws<ValidationException>(() =>
            parameterizer.ValidateFunctionCalls("SET @keyword = trim(@keyword); RETURN @keyword;"));
    }

    [Fact]
    public void ValidateFunctionCalls_RejectsUnknownRuntimeNamespace()
    {
        var parameterizer = CreateParameterizer();

        var exception = Assert.Throws<ValidationException>(() =>
            parameterizer.ValidateFunctionCalls("RETURN SELECT * FROM orders WHERE order_no = BadFns.trim(@keyword);"));

        Assert.Contains("命名空间不在白名单", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateFunctionCalls_RejectsDatabaseColumnAsFunctionArgument()
    {
        var parameterizer = CreateParameterizer();

        var exception = Assert.Throws<ValidationException>(() =>
            parameterizer.ValidateFunctionCalls("RETURN SELECT * FROM orders WHERE order_no = StringFns.trim(order_no);"));

        Assert.Contains("数据库列", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateFunctionCalls_RejectsWrongArgumentCount()
    {
        var parameterizer = CreateParameterizer();

        var exception = Assert.Throws<ValidationException>(() =>
            parameterizer.ValidateFunctionCalls("SET @pageSize = NumberFns.clamp(@pageSize, 1); RETURN @pageSize;"));

        Assert.Contains("参数数量错误", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IfElseBlockReader_ReadsConditionWithNestedFunctionCall()
    {
        var reader = new ApplicationDataCenterSqlScriptIfElseBlockReader();

        var found = reader.TryReadFirst(
            "IF (StringFns.isNotBlank(@keyword)) { SET @ok = true; } ELSE { SET @ok = false; } RETURN @ok;",
            out var block);

        Assert.True(found);
        Assert.Equal("StringFns.isNotBlank(@keyword)", block.Condition.Trim());
        Assert.Contains("SET @ok = true", block.ThenBlock, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SET @ok = false", block.ElseBlock, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IfElseBlockReader_SkipsSqlIfExistsClauses()
    {
        var reader = new ApplicationDataCenterSqlScriptIfElseBlockReader();

        var found = reader.TryReadFirst(
            """
            DROP TABLE IF EXISTS temp.order_scope;
            CREATE TEMP TABLE IF NOT EXISTS order_scope AS SELECT 1 AS id;
            IF (true) { SET @ok = true; } ELSE { SET @ok = false; }
            RETURN @ok;
            """,
            out var block);

        Assert.True(found);
        Assert.Equal("true", block.Condition.Trim());
        Assert.Contains("SET @ok = true", block.ThenBlock, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SET @ok = false", block.ElseBlock, StringComparison.OrdinalIgnoreCase);
    }

    private static ApplicationDataCenterSqlScriptFunctionParameterizer CreateParameterizer(ICurrentUser? currentUser = null)
    {
        var tokenizer = new ApplicationDataCenterSqlScriptExpressionTokenizer();
        var parser = new ApplicationDataCenterSqlScriptExpressionParser(tokenizer);
        return new ApplicationDataCenterSqlScriptFunctionParameterizer(parser, CreateEvaluator(parser, currentUser));
    }

    private static ApplicationDataCenterSqlScriptExpressionEvaluator CreateEvaluator(ICurrentUser? currentUser = null)
    {
        var tokenizer = new ApplicationDataCenterSqlScriptExpressionTokenizer();
        var parser = new ApplicationDataCenterSqlScriptExpressionParser(tokenizer);
        return CreateEvaluator(parser, currentUser);
    }

    private static ApplicationDataCenterSqlScriptExpressionEvaluator CreateEvaluator(
        ApplicationDataCenterSqlScriptExpressionParser parser,
        ICurrentUser? currentUser = null) =>
        new(
            parser,
            new RuntimeExpressionFunctionCatalog(),
            new RuntimeExpressionHelperCatalog(),
            currentUser is null ? null : new ApplicationDataCenterSqlRbacFunctionEvaluator(currentUser));

    private static ICurrentUser CreateCurrentUser(IReadOnlyList<string> permissions, IReadOnlyList<string> roles)
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
            roles,
            permissions,
            "ALL",
            true,
            true,
            true,
            "平台管理员"));
        return new FixedAsterErpCurrentUser(principal);
    }
}
