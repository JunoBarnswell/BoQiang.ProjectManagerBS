using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Domain.System.Users;

public static class UserDomainPolicy
{
    public static void EnsureCreateRequest(string userName, string displayName, string password, string status)
    {
        EnsureBaseRequest(userName, displayName, status);
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ValidationException("密码不能为空");
        }
    }

    public static void EnsureUpdateRequest(string userName, string displayName, string status)
    {
        EnsureBaseRequest(userName, displayName, status);
    }

    public static string NormalizeStatus(string status)
    {
        var normalized = status.Trim();
        return normalized switch
        {
            "Enabled" => "Enabled",
            "Disabled" => "Disabled",
            _ => throw new ValidationException("状态不合法")
        };
    }

    public static string ResolveDataScope(bool isAdmin, IReadOnlyCollection<string> roleScopes)
    {
        if (isAdmin)
        {
            return "ALL";
        }

        if (roleScopes.Count == 0)
        {
            return "SELF";
        }

        var rank = roleScopes.Select(GetDataScopeRank).DefaultIfEmpty(1).Max();
        return rank switch
        {
            4 => "ALL",
            3 => "DEPT_AND_CHILD",
            2 => "DEPT",
            1 => "SELF",
            _ => "SELF"
        };
    }

    private static void EnsureBaseRequest(string userName, string displayName, string status)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new ValidationException("用户名不能为空");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ValidationException("显示名称不能为空");
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ValidationException("状态不能为空");
        }
    }

    private static int GetDataScopeRank(string dataScope)
    {
        return dataScope.ToUpperInvariant() switch
        {
            "ALL" => 4,
            "DEPT_AND_CHILD" => 3,
            "DEPT" => 2,
            "SELF" => 1,
            _ => 1
        };
    }
}
