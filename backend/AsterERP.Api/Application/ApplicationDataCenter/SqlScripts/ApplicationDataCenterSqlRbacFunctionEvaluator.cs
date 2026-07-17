using System.Text.RegularExpressions;
using AsterERP.Api.Application.Runtime.ExpressionFunctions;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;

public sealed partial class ApplicationDataCenterSqlRbacFunctionEvaluator(ICurrentUser currentUser)
{
    public static bool IsRbacFunction(ApplicationDataCenterSqlScriptFunctionCallExpression function) =>
        string.Equals(function.NamespaceName, RbacExpressionFunctions.NamespaceName, StringComparison.OrdinalIgnoreCase);

    public int Evaluate(
        ApplicationDataCenterSqlScriptFunctionCallExpression function,
        RuntimeExpressionFunctionDefinitionDto definition)
    {
        if (!IsRbacFunction(function))
        {
            throw new ValidationException(
                $"SQL RBAC 函数命名空间无效: {function.QualifiedName}",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var codes = ReadSafeLiteralCodes(function);
        return NormalizeFunctionName(definition.FunctionName) switch
        {
            "haspermission" => ToSqlBoolean(HasPermission(codes[0])),
            "hasanypermission" => ToSqlBoolean(codes.Any(HasPermission)),
            "hasrole" => ToSqlBoolean(HasRole(codes[0])),
            "hasanyrole" => ToSqlBoolean(codes.Any(HasRole)),
            "indept" => ToSqlBoolean(codes.Any(IsInDepartment)),
            "inposition" => ToSqlBoolean(codes.Any(IsInPosition)),
            _ => throw new ValidationException(
                $"SQL RBAC 函数不支持: {function.QualifiedName}",
                ErrorCodes.ApplicationDataCenterInvalidConfig)
        };
    }

    private bool HasPermission(string permissionCode) =>
        currentUser.HasAsterErpPermission(permissionCode);

    private bool HasRole(string roleCode)
    {
        var roles = currentUser.GetAsterErpRoleCodes();
        return roles.Contains(roleCode, StringComparer.OrdinalIgnoreCase) ||
               currentUser.IsInRole(roleCode);
    }

    private bool IsInDepartment(string deptId) =>
        currentUser.GetAsterErpDeptIds().Contains(deptId, StringComparer.OrdinalIgnoreCase);

    private bool IsInPosition(string positionId) =>
        currentUser.GetAsterErpPositionIds().Contains(positionId, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string> ReadSafeLiteralCodes(ApplicationDataCenterSqlScriptFunctionCallExpression function)
    {
        var codes = new List<string>();
        foreach (var argument in function.Arguments)
        {
            if (argument is not ApplicationDataCenterSqlScriptLiteralExpression { Value: string rawCode })
            {
                throw new ValidationException(
                    $"SQL RBAC 函数 {function.QualifiedName} 只允许使用字符串字面量参数",
                    ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            var code = rawCode.Trim();
            if (string.IsNullOrWhiteSpace(code) || !SafeRbacCodeRegex().IsMatch(code))
            {
                throw new ValidationException(
                    $"SQL RBAC 函数 {function.QualifiedName} 参数包含非法权限或角色编码: {rawCode}",
                    ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            codes.Add(code);
        }

        if (codes.Count == 0)
        {
            throw new ValidationException(
                $"SQL RBAC 函数 {function.QualifiedName} 至少需要一个权限或角色编码",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return codes;
    }

    private static string NormalizeFunctionName(string value) =>
        value.Trim().Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static int ToSqlBoolean(bool value) => value ? 1 : 0;

    [GeneratedRegex("^[A-Za-z0-9:_.*-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeRbacCodeRegex();
}
