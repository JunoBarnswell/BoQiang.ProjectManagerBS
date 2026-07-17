using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter;

internal static class ApplicationDevelopmentPageCodeGenerator
{
    public static async Task<string> GenerateAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string pageName,
        CancellationToken cancellationToken)
    {
        var prefix = BuildPrefix(pageName);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = $"{prefix}_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Random.Shared.Next(1000, 9999)}";
            var exists = await db.Queryable<ApplicationDevelopmentPageEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.PageCode == code && !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        throw new ValidationException("页面编码自动生成失败，请重试", ErrorCodes.ApplicationDataCenterDuplicateCode);
    }

    private static string BuildPrefix(string pageName)
    {
        var chars = pageName
            .Trim()
            .Select(ch => char.IsAsciiLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_')
            .ToArray();
        var normalized = new string(chars).Trim('_');
        if (string.IsNullOrWhiteSpace(normalized) || !char.IsAsciiLetter(normalized[0]))
        {
            return "page";
        }

        return normalized.Length > 64 ? normalized[..64] : normalized;
    }
}
