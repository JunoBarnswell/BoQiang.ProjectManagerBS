using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Domain.System.Organizations;

public static class PositionDomainPolicy
{
    public static void EnsureUpsertRequest(string positionCode, string positionName, string deptId)
    {
        if (string.IsNullOrWhiteSpace(positionCode))
        {
            throw new ValidationException("岗位编码不能为空");
        }

        if (string.IsNullOrWhiteSpace(positionName))
        {
            throw new ValidationException("岗位名称不能为空");
        }

        if (string.IsNullOrWhiteSpace(deptId))
        {
            throw new ValidationException("所属部门不能为空");
        }
    }

    public static void EnsureDepartmentRequired(string deptId)
    {
        if (string.IsNullOrWhiteSpace(deptId))
        {
            throw new ValidationException("所属部门不能为空");
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

    public static void EnsureDeletable(bool hasUsers)
    {
        if (hasUsers)
        {
            throw new ValidationException("岗位已被用户引用，不能删除", ErrorCodes.PositionInUse);
        }
    }
}
