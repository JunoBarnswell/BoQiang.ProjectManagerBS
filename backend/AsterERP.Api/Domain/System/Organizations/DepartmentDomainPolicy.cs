using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Domain.System.Organizations;

public static class DepartmentDomainPolicy
{
    public static void EnsureUpsertRequest(string deptCode, string deptName)
    {
        if (string.IsNullOrWhiteSpace(deptCode))
        {
            throw new ValidationException("部门编码不能为空");
        }

        if (string.IsNullOrWhiteSpace(deptName))
        {
            throw new ValidationException("部门名称不能为空");
        }
    }

    public static string NormalizeStatus(string status)
    {
        return status.Trim() switch
        {
            "Enabled" => "Enabled",
            "Disabled" => "Disabled",
            _ => throw new ValidationException("状态不合法")
        };
    }

    public static void EnsureNotSelfParent(string? parentId, string? currentId)
    {
        if (!string.IsNullOrWhiteSpace(parentId) && !string.IsNullOrWhiteSpace(currentId) &&
            string.Equals(parentId.Trim(), currentId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("部门不能选择自身作为上级部门");
        }
    }

    public static void EnsureDeletable(bool hasChildren, bool hasPositions, bool hasUsers)
    {
        if (hasChildren)
        {
            throw new ValidationException("存在下级部门，不能删除", ErrorCodes.DepartmentInUse);
        }

        if (hasPositions)
        {
            throw new ValidationException("部门已被岗位引用，不能删除", ErrorCodes.DepartmentInUse);
        }

        if (hasUsers)
        {
            throw new ValidationException("部门已被用户引用，不能删除", ErrorCodes.DepartmentInUse);
        }
    }
}
