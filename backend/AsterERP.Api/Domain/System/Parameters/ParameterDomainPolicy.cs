using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Domain.System.Parameters;

public static class ParameterDomainPolicy
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Enabled",
        "Disabled"
    };

    public static void EnsureUpsertRequest(string paramName, string paramKey, string paramValue, string category)
    {
        if (string.IsNullOrWhiteSpace(paramName))
        {
            throw new ValidationException("参数名称不能为空");
        }

        if (string.IsNullOrWhiteSpace(paramKey))
        {
            throw new ValidationException("参数键名不能为空");
        }

        if (string.IsNullOrWhiteSpace(paramValue))
        {
            throw new ValidationException("参数键值不能为空");
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ValidationException("参数分类不能为空");
        }
    }

    public static bool ToEnabledStatus(string status)
    {
        if (!ValidStatuses.Contains(status.Trim()))
        {
            throw new ValidationException("参数状态不合法");
        }

        return status.Trim().Equals("Enabled", StringComparison.OrdinalIgnoreCase);
    }
}
