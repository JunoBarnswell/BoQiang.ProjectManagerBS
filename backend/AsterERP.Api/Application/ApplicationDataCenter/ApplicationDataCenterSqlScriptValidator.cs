using System.Text.RegularExpressions;
using AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;
using AsterERP.Contracts.ApplicationDataCenter;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed partial class ApplicationDataCenterSqlScriptValidator(
    ApplicationDataCenterSqlScriptParser parser,
    ApplicationDataCenterSqlScriptFunctionParameterizer functionParameterizer)
{
    public bool HasConfiguredSqlScript(ApplicationMicroflowSqlScriptDefinition? sqlScript) =>
        sqlScript is not null &&
        (!string.IsNullOrWhiteSpace(sqlScript.Script) ||
         sqlScript.Parameters.Count > 0 ||
         sqlScript.LocalVariables.Count > 0 ||
         sqlScript.ResultShape.Fields.Count > 0);

    public void Validate(
        ApplicationMicroflowNodeDefinition node,
        ApplicationMicroflowOutputSchemaDefinition schema,
        List<string> errors)
    {
        var sqlScript = schema.SqlScript;
        if (!HasConfiguredSqlScript(sqlScript))
        {
            errors.Add($"Return 节点 {node.Name}({node.Id}) SQL 脚本模式缺少脚本配置");
            return;
        }

        ValidateSqlScript(sqlScript!, errors, $"Return 节点 {node.Name}({node.Id})");
        for (var index = 0; index < schema.Fields.Count; index += 1)
        {
            var expression = schema.Fields[index].Expression;
            if (expression?.Kind?.Equals("ref", StringComparison.OrdinalIgnoreCase) != true ||
                !string.Equals(expression.Ref?.SourceType, "sqlResult", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Return 节点 {node.Name}({node.Id}) SQL 脚本模式字段 {schema.Fields[index].FieldCode} 必须绑定 SQL 结果字段");
            }
        }
    }

    public void ValidateSqlScript(ApplicationMicroflowSqlScriptDefinition sqlScript, List<string> errors, string ownerLabel)
    {
        ApplicationDataCenterSqlScriptPlan? plan = null;
        try
        {
            plan = parser.Parse(sqlScript.Script);
            functionParameterizer.ValidateFunctionCalls(sqlScript.Script);
        }
        catch (Exception exception)
        {
            errors.Add($"{ownerLabel} SQL 脚本无效: {exception.Message}");
        }

        var knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ValidateVariableDefinitions(
            "参数",
            sqlScript.Parameters.Select((item, index) => new ScriptVariableDefinition(item.Name, item.DataType, item.Expression is not null, index + 1)),
            knownNames,
            errors,
            ownerLabel);
        ValidateVariableDefinitions(
            "局部变量",
            sqlScript.LocalVariables.Select((item, index) => new ScriptVariableDefinition(item.Name, item.DataType, true, index + 1)),
            knownNames,
            errors,
            ownerLabel);
        ValidateReservedControlTargets(sqlScript.Script, errors, ownerLabel);

        if (plan is null)
        {
            return;
        }

        foreach (var name in plan.DeclaredVariableNames)
        {
            if (ApplicationDataCenterSqlBuiltInVariableNames.IsReserved(name))
            {
                errors.Add($"{ownerLabel} SQL 脚本不能声明内置变量 @{name}");
            }
        }

        knownNames.UnionWith(plan.DeclaredVariableNames);
        foreach (var name in parser.ExtractReferencedVariableNames(sqlScript.Script))
        {
            if (!knownNames.Contains(name))
            {
                knownNames.Add(name);
            }
        }
    }

    public static string NormalizeName(string? value) =>
        (value ?? string.Empty).Trim().TrimStart('@');

    private static void ValidateVariableDefinitions(
        string label,
        IEnumerable<ScriptVariableDefinition> variables,
        HashSet<string> knownNames,
        List<string> errors,
        string ownerLabel)
    {
        foreach (var variable in variables)
        {
            var name = NormalizeName(variable.Name);
            if (string.IsNullOrWhiteSpace(name) || !VariableNameRegex().IsMatch(name))
            {
                errors.Add($"{ownerLabel} SQL 脚本第 {variable.Index} 个{label}名无效，只允许字母、数字和下划线");
                continue;
            }

            if (ApplicationDataCenterSqlBuiltInVariableNames.IsReserved(name))
            {
                errors.Add($"{ownerLabel} SQL 脚本{label} @{name} 是系统内置变量，不能由脚本声明或覆盖");
                continue;
            }

            if (!knownNames.Add(name))
            {
                errors.Add($"{ownerLabel} SQL 脚本变量名重复: @{name}");
            }

            if (string.IsNullOrWhiteSpace(variable.DataType))
            {
                errors.Add($"{ownerLabel} SQL 脚本{label} @{name} 缺少数据类型");
            }

            if (!variable.HasExpression)
            {
                errors.Add($"{ownerLabel} SQL 脚本{label} @{name} 缺少变量来源");
            }
        }
    }

    private static void ValidateReservedControlTargets(string script, List<string> errors, string ownerLabel)
    {
        var inspect = ApplicationDataCenterSqlScriptParser.RemoveStringLiteralsAndComments(script);
        foreach (Match match in SetTargetRegex().Matches(inspect))
        {
            var name = match.Groups["name"].Value;
            if (ApplicationDataCenterSqlBuiltInVariableNames.IsReserved(name))
            {
                errors.Add($"{ownerLabel} SQL 脚本不能给内置变量 @{name} 赋值");
            }
        }

        foreach (Match match in ForItemRegex().Matches(inspect))
        {
            var name = match.Groups["name"].Value;
            if (ApplicationDataCenterSqlBuiltInVariableNames.IsReserved(name))
            {
                errors.Add($"{ownerLabel} SQL 脚本不能使用内置变量 @{name} 作为 FOR 循环变量");
            }
        }
    }

    private sealed record ScriptVariableDefinition(string Name, string DataType, bool HasExpression, int Index);

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex VariableNameRegex();

    [GeneratedRegex("\\bSET\\s+@(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SetTargetRegex();

    [GeneratedRegex("\\bFOR\\s+@(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s+IN\\s+@", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForItemRegex();
}
