using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Platform;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.Platform.Applications;

public sealed class PlatformApplicationWorkspaceProvisioningService(
    ISqlSugarClient db,
    ICurrentUser currentUser,
    PlatformAccessGuard accessGuard) : IPlatformApplicationWorkspaceProvisioningService
{
    private const string PlatformAppCode = "SYSTEM";

    public async Task ProvisionCurrentTenantAsync(
        string appCode,
        string appName,
        CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var normalizedAppCode = NormalizeAppCode(appCode);
        if (string.Equals(normalizedAppCode, PlatformAppCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var tenantId = NormalizeRequired(currentUser.GetAsterErpTenantId(), "当前平台租户不能为空");
        var tenant = (await db.Queryable<SystemTenantEntity>()
            .Where(item =>
                item.Id == tenantId &&
                !item.IsDeleted &&
                item.Status == "Enabled" &&
                (item.ExpiredAt == null || item.ExpiredAt > DateTime.UtcNow))
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new ValidationException("当前平台租户不存在或已停用", ErrorCodes.PermissionDenied);

        var applicationExists = await db.Queryable<SystemApplicationEntity>()
            .Where(item =>
                item.AppCode == normalizedAppCode &&
                !item.IsDeleted &&
                item.Status == "Enabled")
            .AnyAsync(cancellationToken);
        if (!applicationExists)
        {
            throw new ValidationException("应用不存在或已停用", ErrorCodes.PlatformResourceNotFound);
        }

        var existing = (await db.Queryable<SystemTenantAppEntity>()
            .Where(item => item.TenantId == tenant.Id && item.AppCode == normalizedAppCode)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        var now = DateTime.UtcNow;
        var userId = currentUser.GetAsterErpUserId();
        if (existing is null)
        {
            await db.Insertable(new SystemTenantAppEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                TenantId = tenant.Id,
                AppCode = normalizedAppCode,
                Status = "Enabled",
                SystemName = ResolveSystemName(tenant.TenantName, appName),
                CreatedBy = userId,
                CreatedTime = now,
                IsDeleted = false
            }).ExecuteCommandAsync(cancellationToken);
            return;
        }

        existing.Status = "Enabled";
        existing.SystemName = string.IsNullOrWhiteSpace(existing.SystemName)
            ? ResolveSystemName(tenant.TenantName, appName)
            : existing.SystemName;
        existing.IsDeleted = false;
        existing.DeletedBy = null;
        existing.DeletedTime = null;
        existing.UpdatedBy = userId;
        existing.UpdatedTime = now;
        await db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
    }

    private static string NormalizeAppCode(string value) =>
        NormalizeRequired(value, "应用编码不能为空").ToUpperInvariant();

    private static string NormalizeRequired(string? value, string message)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ValidationException(message)
            : normalized;
    }

    private static string ResolveSystemName(string tenantName, string appName) =>
        $"{tenantName} {appName.Trim()}";
}
