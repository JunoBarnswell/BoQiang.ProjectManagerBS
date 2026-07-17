using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.Platform;
using AsterERP.Api.Application.Common;
using AsterERP.Api.Modules.Platform;
using SqlSugar;

namespace AsterERP.Api.Application.Platform.TenantApps;

public sealed class PlatformTenantAppService(
    ISqlSugarClient db,
    PlatformAccessGuard accessGuard) : IPlatformTenantAppService
{
    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemTenantAppEntity, SystemTenantEntity, SystemApplicationEntity>, GridFilter, ISugarQueryable<SystemTenantAppEntity, SystemTenantEntity, SystemApplicationEntity>>> Filterers =
        new Dictionary<string, Func<ISugarQueryable<SystemTenantAppEntity, SystemTenantEntity, SystemApplicationEntity>, GridFilter, ISugarQueryable<SystemTenantAppEntity, SystemTenantEntity, SystemApplicationEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["appCode"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (tenantApp, tenant, app) => tenantApp.AppCode),
            ["appName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (tenantApp, tenant, app) => app.AppName),
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, (tenantApp, tenant, app) => tenantApp.CreatedTime),
            ["expiredAt"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, (tenantApp, tenant, app) => tenantApp.ExpiredAt),
            ["status"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (tenantApp, tenant, app) => tenantApp.Status),
            ["systemName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (tenantApp, tenant, app) => tenantApp.SystemName),
            ["tenantName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (tenantApp, tenant, app) => tenant.TenantName),
            ["updatedTime"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, (tenantApp, tenant, app) => tenantApp.UpdatedTime)
        };

    public async Task<GridPageResult<TenantAppListItemResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var tenantId = NormalizeOptional(gridQuery.TenantId);
        var appCode = NormalizeOptional(gridQuery.AppCode)?.ToUpperInvariant();
        var status = NormalizeOptional(gridQuery.Status);
        var keyword = NormalizeOptional(gridQuery.Keyword);

        var query = db.Queryable<SystemTenantAppEntity, SystemTenantEntity, SystemApplicationEntity>(
                (tenantApp, tenant, app) => tenantApp.TenantId == tenant.Id && tenantApp.AppCode == app.AppCode)
            .Where((tenantApp, tenant, app) => !tenantApp.IsDeleted && !tenant.IsDeleted && !app.IsDeleted);

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            query = query.Where((tenantApp, tenant, app) => tenantApp.TenantId == tenantId);
        }

        if (!string.IsNullOrWhiteSpace(appCode))
        {
            query = query.Where((tenantApp, tenant, app) => tenantApp.AppCode == appCode);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where((tenantApp, tenant, app) => tenantApp.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where((tenantApp, tenant, app) =>
                tenant.TenantName.Contains(keyword) ||
                app.AppName.Contains(keyword) ||
                tenantApp.AppCode.Contains(keyword) ||
                (tenantApp.SystemName != null && tenantApp.SystemName.Contains(keyword)));
        }

        query = GridFilterApplier.Apply(query, gridQuery.Filters, Filterers);

        var total = new RefAsync<int>();
        var items = await GridSortApplier
            .Apply(
                query,
                gridQuery.Sorts,
                (nextQuery, field, order) => field switch
                {
                    "appCode" => nextQuery.OrderBy((tenantApp, tenant, app) => tenantApp.AppCode, order),
                    "appName" => nextQuery.OrderBy((tenantApp, tenant, app) => app.AppName, order),
                    "createdTime" => nextQuery.OrderBy((tenantApp, tenant, app) => tenantApp.CreatedTime, order),
                    "expiredAt" => nextQuery.OrderBy((tenantApp, tenant, app) => tenantApp.ExpiredAt, order),
                    "primaryColor" => nextQuery.OrderBy((tenantApp, tenant, app) => tenantApp.PrimaryColor, order),
                    "status" => nextQuery.OrderBy((tenantApp, tenant, app) => tenantApp.Status, order),
                    "systemName" => nextQuery.OrderBy((tenantApp, tenant, app) => tenantApp.SystemName, order),
                    "tenantName" => nextQuery.OrderBy((tenantApp, tenant, app) => tenant.TenantName, order),
                    "updatedTime" => nextQuery.OrderBy((tenantApp, tenant, app) => tenantApp.UpdatedTime, order),
                    _ => null
                },
                nextQuery => nextQuery.OrderBy((tenantApp, tenant, app) => tenantApp.CreatedTime, OrderByType.Desc))
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
            .ToPageListAsync(gridQuery.PageIndex, gridQuery.PageSize, total);

        return new GridPageResult<TenantAppListItemResponse> { Total = total.Value, Items = items };
    }

    public async Task<TenantAppListItemResponse> CreateAsync(TenantAppUpsertRequest request, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        EnsureRequest(request);
        var tenant = await GetEnabledTenantAsync(request.TenantId, cancellationToken);
        var app = await GetEnabledApplicationAsync(request.AppCode, cancellationToken);
        await EnsureUniqueAsync(request.TenantId, app.AppCode, null, cancellationToken);

        var entity = new SystemTenantAppEntity
        {
            TenantId = tenant.Id,
            AppCode = app.AppCode,
            Status = NormalizeStatus(request.Status),
            SystemName = NormalizeOptional(request.SystemName),
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

    public async Task<TenantAppListItemResponse> UpdateAsync(string id, TenantAppUpsertRequest request, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        EnsureRequest(request);
        var entity = await GetRequiredAsync(id, cancellationToken);
        var tenant = await GetEnabledTenantAsync(request.TenantId, cancellationToken);
        var app = await GetEnabledApplicationAsync(request.AppCode, cancellationToken);
        await EnsureUniqueAsync(request.TenantId, app.AppCode, id, cancellationToken);

        entity.TenantId = tenant.Id;
        entity.AppCode = app.AppCode;
        entity.Status = NormalizeStatus(request.Status);
        entity.SystemName = NormalizeOptional(request.SystemName);
        entity.LogoFileId = NormalizeOptional(request.LogoFileId);
        entity.FaviconFileId = NormalizeOptional(request.FaviconFileId);
        entity.PrimaryColor = NormalizeOptional(request.PrimaryColor);
        entity.ExpiredAt = request.ExpiredAt;
        entity.ConfigJson = NormalizeOptional(request.ConfigJson);
        entity.Remark = NormalizeOptional(request.Remark);
        entity.UpdatedTime = DateTime.UtcNow;

        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return await MapAsync(entity.Id, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var entity = await GetRequiredAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        entity.UpdatedTime = entity.DeletedTime;
        await db.Updateable(entity).UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime }).ExecuteCommandAsync(cancellationToken);
    }

    public async Task BatchUpdateStatusAsync(IReadOnlyList<string> ids, string status, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var normalizedIds = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (normalizedIds.Count == 0)
        {
            return;
        }

        var entities = await db.Queryable<SystemTenantAppEntity>()
            .Where(item => normalizedIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (entities.Count != normalizedIds.Count)
        {
            throw new NotFoundException("租户应用不存在", ErrorCodes.PlatformResourceNotFound);
        }

        var normalizedStatus = NormalizeStatus(status);
        var updatedTime = DateTime.UtcNow;
        foreach (var entity in entities)
        {
            entity.Status = normalizedStatus;
            entity.UpdatedTime = updatedTime;
        }

        await db.Updateable(entities).UpdateColumns(item => new { item.Status, item.UpdatedTime }).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<TenantAppListItemResponse> MapAsync(string id, CancellationToken cancellationToken)
    {
        var tenantApp = await GetRequiredAsync(id, cancellationToken);
        var tenant = (await db.Queryable<SystemTenantEntity>()
            .Where(item => item.Id == tenantApp.TenantId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("租户不存在", ErrorCodes.PlatformResourceNotFound);
        var app = (await db.Queryable<SystemApplicationEntity>()
            .Where(item => item.AppCode == tenantApp.AppCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("应用不存在", ErrorCodes.PlatformResourceNotFound);

        return new TenantAppListItemResponse(
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
            tenantApp.Remark);
    }

    private async Task<SystemTenantAppEntity> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        return (await db.Queryable<SystemTenantAppEntity>()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("租户应用不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task<SystemTenantEntity> GetEnabledTenantAsync(string tenantId, CancellationToken cancellationToken)
    {
        return (await db.Queryable<SystemTenantEntity>()
            .Where(item => item.Id == tenantId.Trim() && !item.IsDeleted && item.Status == "Enabled")
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new ValidationException("租户不存在或已停用");
    }

    private async Task<SystemApplicationEntity> GetEnabledApplicationAsync(string appCode, CancellationToken cancellationToken)
    {
        var normalizedCode = appCode.Trim().ToUpperInvariant();
        return (await db.Queryable<SystemApplicationEntity>()
            .Where(item => item.AppCode == normalizedCode && !item.IsDeleted && item.Status == "Enabled")
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new ValidationException("应用不存在或已停用");
    }

    private async Task EnsureUniqueAsync(string tenantId, string appCode, string? currentId, CancellationToken cancellationToken)
    {
        var exists = await db.Queryable<SystemTenantAppEntity>()
            .Where(item => item.TenantId == tenantId.Trim() && item.AppCode == appCode && item.Id != (currentId ?? string.Empty) && !item.IsDeleted)
            .AnyAsync(cancellationToken);
        if (exists)
        {
            throw new ValidationException("该租户已安装此应用");
        }
    }

    private static void EnsureRequest(TenantAppUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.AppCode))
        {
            throw new ValidationException("租户和应用不能为空");
        }
    }

    private static string NormalizeStatus(string status)
    {
        return status.Trim().Equals("Disabled", StringComparison.OrdinalIgnoreCase) ? "Disabled" : "Enabled";
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
