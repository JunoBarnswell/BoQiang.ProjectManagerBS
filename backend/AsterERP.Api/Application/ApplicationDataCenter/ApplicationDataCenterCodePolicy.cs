using System.Text.RegularExpressions;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public static partial class ApplicationDataCenterCodePolicy
{
    public static string NormalizeCode(string value, string displayName)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 128)
        {
            throw new ValidationException($"{displayName}不能为空且长度不能超过 128", ErrorCodes.ParameterInvalid);
        }

        if (!CodeRegex().IsMatch(normalized))
        {
            throw new ValidationException($"{displayName}只能包含字母、数字、下划线、中划线、点和冒号，且必须以字母开头", ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }

    public static string NormalizeName(string value, string displayName)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 128)
        {
            throw new ValidationException($"{displayName}不能为空且长度不能超过 128", ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }

    public static string? NormalizeOptional(string? value, int maxLength = 255)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new ValidationException($"字段长度不能超过 {maxLength}", ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_.:-]*$")]
    private static partial Regex CodeRegex();
}
