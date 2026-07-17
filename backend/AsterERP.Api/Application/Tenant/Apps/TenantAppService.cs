using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.Platform;
using AsterERP.Contracts.Tenant;
using AsterERP.Api.Modules.Platform;
using SqlSugar;

namespace AsterERP.Api.Application.Tenant.Apps;

public sealed class TenantAppService(
    ISqlSugarClient db,
    TenantAccessGuard accessGuard) : ITenantAppService
{
    public async Task<IReadOnlyList<TenantAppCatalogItemResponse>> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = accessGuard.GetTenantIdForTenantAdmin();
        var apps = await db.Queryable<SystemApplicationEntity>()
            .Where(item => !item.IsDeleted && item.Status == "Enabled")
            .OrderBy(item => item.AppCode)
            .ToListAsync(cancellationToken);

        var tenantApps = await db.Queryable<SystemTenantAppEntity>()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var installedByApp = tenantApps.ToDictionary(item => item.AppCode, StringComparer.OrdinalIgnoreCase);

        return apps.Select(app =>
        {
            installedByApp.TryGetValue(app.AppCode, out var tenantApp);
            return new TenantAppCatalogItemResponse(
                app.AppCode,
                app.AppName,
                app.AppType,
                app.Icon,
                app.DefaultRoutePath,
                app.Version,
                tenantApp is not null,
                tenantApp?.Id,
                tenantApp?.Status,
                tenantApp?.SystemName,
                tenantApp?.PrimaryColor,
                tenantApp?.ExpiredAt);
        }).ToList();
    }

    public async Task<IReadOnlyList<TenantAppListItemResponse>> GetInstalledAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = accessGuard.GetTenantIdForTenantAdmin();
        var items = await db.Queryable<SystemTenantAppEntity, SystemTenantEntity, SystemApplicationEntity>(
                (tenantApp, tenant, app) => tenantApp.TenantId == tenant.Id && tenantApp.AppCode == app.AppCode)
            .Where((tenantApp, tenant, app) =>
                tenantApp.TenantId == tenantId &&
                !tenantApp.IsDeleted &&
                !tenant.IsDeleted &&
                !app.IsDeleted)
            .OrderBy((tenantApp, tenant, app) => tenantApp.CreatedTime, OrderByType.Desc)
            .Select((tenantApp, tenant, app) => new TenantAppListItemResponse(
                tenantApp.Id,
                tenantApp.TenantId,
                tenant.TenantName,
                tenantApp.AppCode,
                app.AppName,
                tenantApp.Status,
                tenantApp.SystemName,
                tenantApp.LogoFileId,
                tenantApp.FaviconFileId,
                tenantApp.PrimaryColor,
                tenantApp.ExpiredAt,
                tenantApp.ConfigJson,
                tenantApp.Remark))
            .ToListAsync(cancellationToken);

        return items;
    }

    public async Task<TenantAppListItemResponse> InstallAsync(string appCode, TenantAppInstallRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = accessGuard.GetTenantIdForTenantAdmin();
        var app = await GetEnabledApplicationAsync(appCode, cancellationToken);
        var existing = await db.Queryable<SystemTenantAppEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == app.AppCode && !item.IsDeleted)
            .FirstAsync(cancellationToken);
        if (existing is not null)
        {
            throw new ValidationException("该租户已安装此应用");
        }

        var entity = new SystemTenantAppEntity
        {
            TenantId = tenantId,
            AppCode = app.AppCode,
            Status = "Enabled",
            SystemName = NormalizeOptional(request.SystemName) ?? app.AppName,
            LogoFileId = NormalizeOptional(request.LogoFileId),
            FaviconFileId = NormalizeOptional(request.FaviconFileId),
            PrimaryColor = NormalizeOptional(request.PrimaryColor),
            ExpiredAt = request.ExpiredAt,
            ConfigJson = NormalizeOptional(request.ConfigJson),
            Remark = NormalizeOptional(request.Remark)
        };

        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return await MapAsync(entity.Id, cancellationToken);
    }

    public async Task<TenantAppListItemResponse> EnableAsync(string appCode, CancellationToken cancellationToken = default)
    {
        var tenantApp = await GetCurrentTenantAppAsync(appCode, cancellationToken);
        tenantApp.Status = "Enabled";
        tenantApp.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(tenantApp)
            .UpdateColumns(item => new { item.Status, item.UpdatedTime })
            .ExecuteCommandAsync(cancellationToken);
        return await MapAsync(tenantApp.Id, cancellationToken);
    }

    public async Task<TenantAppListItemResponse> DisableAsync(string appCode, CancellationToken cancellationToken = default)
    {
        var tenantApp = await GetCurrentTenantAppAsync(appCode, cancellationToken);
        tenantApp.Status = "Disabled";
        tenantApp.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(tenantApp)
            .UpdateColumns(item => new { item.Status, item.UpdatedTime })
            .ExecuteCommandAsync(cancellationToken);
        return await MapAsync(tenantApp.Id, cancellationToken);
    }

    private async Task<SystemTenantAppEntity> GetCurrentTenantAppAsync(string appCode, CancellationToken cancellationToken)
    {
        var tenantId = accessGuard.GetTenantIdForTenantAdmin();
        var normalizedAppCode = NormalizeAppCode(appCode);
        return await db.Queryable<SystemTenantAppEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == normalizedAppCode && !item.IsDeleted)
            .FirstAsync(cancellationToken)
            ?? throw new NotFoundException("租户应用不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task<SystemApplicationEntity> GetEnabledApplicationAsync(string appCode, CancellationToken cancellationToken)
    {
        var normalizedAppCode = NormalizeAppCode(appCode);
        return await db.Queryable<SystemApplicationEntity>()
            .Where(item => item.AppCode == normalizedAppCode && item.Status == "Enabled" && !item.IsDeleted)
            .FirstAsync(cancellationToken)
            ?? throw new ValidationException("应用不存在或未发布");
    }

    private async Task<TenantAppListItemResponse> MapAsync(string id, CancellationToken cancellationToken)
    {
        return await db.Queryable<SystemTenantAppEntity, SystemTenantEntity, SystemApplicationEntity>(
                (tenantApp, tenant, app) => tenantApp.TenantId == tenant.Id && tenantApp.AppCode == app.AppCode)
            .Where((tenantApp, tenant, app) => tenantApp.Id == id && !tenantApp.IsDeleted && !tenant.IsDeleted && !app.IsDeleted)
            .Select((tenantApp, tenant, app) => new TenantAppListItemResponse(
                tenantApp.Id,
                tenantApp.TenantId,
                tenant.TenantName,
                tenantApp.AppCode,
                app.AppName,
                tenantApp.Status,
                tenantApp.SystemName,
                tenantApp.LogoFileId,
                tenantApp.FaviconFileId,
                tenantApp.PrimaryColor,
                tenantApp.ExpiredAt,
                tenantApp.ConfigJson,
                tenantApp.Remark))
            .FirstAsync(cancellationToken)
            ?? throw new NotFoundException("租户应用不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private static string NormalizeAppCode(string appCode)
    {
        if (string.IsNullOrWhiteSpace(appCode))
        {
            throw new ValidationException("应用编码不能为空");
        }

        return appCode.Trim().ToUpperInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
