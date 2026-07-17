using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Domain.System.Roles;

public static class RoleDomainPolicy
{
    public static void EnsureUpsertRequest(string roleName, string roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            throw new ValidationException("角色名称不能为空");
        }

        if (string.IsNullOrWhiteSpace(roleCode))
        {
            throw new ValidationException("角色编码不能为空");
        }
    }

    public static void EnsureDeletable(bool boundToUser)
    {
        if (boundToUser)
        {
            throw new ValidationException("角色已分配给用户，不能删除", ErrorCodes.RoleBoundToUser);
        }
    }

    public static string NormalizeDataScope(string dataScope)
    {
        var normalized = dataScope.Trim().ToUpperInvariant();
        return normalized switch
        {
            "ALL" => "ALL",
            "DEPT_AND_CHILD" => "DEPT_AND_CHILD",
            "DEPT" => "DEPT",
            "SELF" => "SELF",
            "CUSTOM" => "CUSTOM",
            _ => throw new ValidationException("数据范围不合法")
        };
    }
}
