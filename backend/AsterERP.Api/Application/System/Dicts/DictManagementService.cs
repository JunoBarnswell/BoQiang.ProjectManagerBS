using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.System.Dicts;
using AsterERP.Api.Application.Common;
using AsterERP.Api.Domain.System.Dicts;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Infrastructure.UnitOfWork;
using AsterERP.Api.Modules.System.Dicts;
using SqlSugar;

namespace AsterERP.Api.Application.System.Dicts;

public sealed class DictManagementService(
    IRepository<SystemDictTypeEntity> dictTypeRepository,
    IRepository<SystemDictItemEntity> dictItemRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : IDictManagementService
{
    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemDictTypeEntity>, OrderByType, ISugarQueryable<SystemDictTypeEntity>>> TypeSorters =
        new Dictionary<string, Func<ISugarQueryable<SystemDictTypeEntity>, OrderByType, ISugarQueryable<SystemDictTypeEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdTime"] = (query, order) => query.OrderBy(item => item.CreatedTime, order),
            ["dictCode"] = (query, order) => query.OrderBy(item => item.DictCode, order),
            ["dictName"] = (query, order) => query.OrderBy(item => item.DictName, order),
            ["isEnabled"] = (query, order) => query.OrderBy(item => item.IsEnabled, order),
            ["updatedTime"] = (query, order) => query.OrderBy(item => item.UpdatedTime, order)
        };

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemDictTypeEntity>, GridFilter, ISugarQueryable<SystemDictTypeEntity>>> TypeFilterers =
        new Dictionary<string, Func<ISugarQueryable<SystemDictTypeEntity>, GridFilter, ISugarQueryable<SystemDictTypeEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, item => item.CreatedTime),
            ["dictCode"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.DictCode),
            ["dictName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.DictName),
            ["isEnabled"] = (query, filter) => GridFilterApplier.ApplyBoolean(query, filter, item => item.IsEnabled),
            ["remark"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Remark),
            ["updatedTime"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, item => item.UpdatedTime)
        };

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemDictItemEntity>, OrderByType, ISugarQueryable<SystemDictItemEntity>>> ItemSorters =
        new Dictionary<string, Func<ISugarQueryable<SystemDictItemEntity>, OrderByType, ISugarQueryable<SystemDictItemEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdTime"] = (query, order) => query.OrderBy(item => item.CreatedTime, order),
            ["isEnabled"] = (query, order) => query.OrderBy(item => item.IsEnabled, order),
            ["itemLabel"] = (query, order) => query.OrderBy(item => item.ItemLabel, order),
            ["itemValue"] = (query, order) => query.OrderBy(item => item.ItemValue, order),
            ["sortOrder"] = (query, order) => query.OrderBy(item => item.SortOrder, order),
            ["updatedTime"] = (query, order) => query.OrderBy(item => item.UpdatedTime, order)
        };

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemDictItemEntity>, GridFilter, ISugarQueryable<SystemDictItemEntity>>> ItemFilterers =
        new Dictionary<string, Func<ISugarQueryable<SystemDictItemEntity>, GridFilter, ISugarQueryable<SystemDictItemEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, item => item.CreatedTime),
            ["dictTypeId"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.DictTypeId),
            ["isEnabled"] = (query, filter) => GridFilterApplier.ApplyBoolean(query, filter, item => item.IsEnabled),
            ["itemLabel"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.ItemLabel),
            ["itemValue"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.ItemValue),
            ["remark"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Remark),
            ["sortOrder"] = (query, filter) => GridFilterApplier.ApplyInt32(query, filter, item => item.SortOrder),
            ["updatedTime"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, item => item.UpdatedTime)
        };

    public async Task<GridPageResult<DictTypeListItemResponse>> GetTypesPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default)
    {
        var keyword = gridQuery.Keyword?.Trim();
        var pageQuery = gridQuery.ToPageQuery();
        var query = dictTypeRepository.Query();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(item => item.DictCode.Contains(keyword) || item.DictName.Contains(keyword));
        }

        query = GridFilterApplier.Apply(query, gridQuery.Filters, TypeFilterers);

        var total = await query.CountAsync(cancellationToken);
        var items = await GridSortApplier
            .Apply(query, gridQuery.Sorts, TypeSorters, ApplyDefaultTypeSort)
            .Skip(pageQuery.SkipCount)
            .Take(Math.Max(pageQuery.PageSize, 1))
            .ToListAsync(cancellationToken);

        return new GridPageResult<DictTypeListItemResponse>
        {
            Total = total,
            Items = items.Select(MapType).ToList()
        };
    }

    public async Task<IReadOnlyList<DictItemListItemResponse>> GetItemsAsync(string dictTypeId, CancellationToken cancellationToken = default)
    {
        await EnsureTypeExistsAsync(dictTypeId, cancellationToken);

        var items = (await dictItemRepository.Query()
            .Where(item => item.DictTypeId == dictTypeId)
            .OrderBy(item => item.SortOrder, OrderByType.Asc)
            .ToListAsync(cancellationToken))
            .Select(MapItem)
            .ToList();

        return items;
    }

    public async Task<GridPageResult<DictItemListItemResponse>> GetItemsPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default)
    {
        var dictTypeId = NormalizeOptional(gridQuery.ParentId);
        if (dictTypeId is null)
        {
            return new GridPageResult<DictItemListItemResponse> { Total = 0, Items = [] };
        }

        await EnsureTypeExistsAsync(dictTypeId, cancellationToken);

        var keyword = gridQuery.Keyword?.Trim();
        var query = dictItemRepository.Query()
            .Where(item => item.DictTypeId == dictTypeId);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(item => item.ItemLabel.Contains(keyword) || item.ItemValue.Contains(keyword));
        }

        query = GridFilterApplier.Apply(query, gridQuery.Filters, ItemFilterers);

        var pageQuery = gridQuery.ToPageQuery();
        var total = await query.CountAsync(cancellationToken);
        var items = await GridSortApplier
            .Apply(query, gridQuery.Sorts, ItemSorters, ApplyDefaultItemSort)
            .Skip(pageQuery.SkipCount)
            .Take(Math.Max(pageQuery.PageSize, 1))
            .ToListAsync(cancellationToken);

        return new GridPageResult<DictItemListItemResponse>
        {
            Total = total,
            Items = items.Select(MapItem).ToList()
        };
    }

    public async Task<DictTypeListItemResponse> GetTypeDetailAsync(string id, CancellationToken cancellationToken = default)
    {
        return MapType(await EnsureTypeExistsAsync(id, cancellationToken));
    }

    public async Task<DictItemListItemResponse> GetItemDetailAsync(string id, CancellationToken cancellationToken = default)
    {
        return MapItem(await EnsureItemExistsAsync(id, cancellationToken));
    }

    public async Task<DictTypeListItemResponse> CreateTypeAsync(DictTypeUpsertRequest request, CancellationToken cancellationToken = default)
    {
        DictDomainPolicy.EnsureTypeRequest(request.DictCode, request.DictName);
        await EnsureTypeCodeUniqueAsync(request.DictCode, null, cancellationToken);

        var entity = new SystemDictTypeEntity
        {
            DictCode = request.DictCode.Trim(),
            DictName = request.DictName.Trim(),
            IsEnabled = request.IsEnabled,
            Remark = request.Remark?.Trim()
        };

        await dictTypeRepository.InsertAsync(entity, cancellationToken);
        return MapType(entity);
    }

    public async Task<DictTypeListItemResponse> UpdateTypeAsync(string id, DictTypeUpsertRequest request, CancellationToken cancellationToken = default)
    {
        DictDomainPolicy.EnsureTypeRequest(request.DictCode, request.DictName);

        var entity = await EnsureTypeExistsAsync(id, cancellationToken);
        await EnsureTypeCodeUniqueAsync(request.DictCode, id, cancellationToken);

        entity.DictCode = request.DictCode.Trim();
        entity.DictName = request.DictName.Trim();
        entity.IsEnabled = request.IsEnabled;
        entity.Remark = request.Remark?.Trim();

        await dictTypeRepository.UpdateAsync(entity, cancellationToken);
        return MapType(entity);
    }

    public async Task DeleteTypeAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await EnsureTypeExistsAsync(id, cancellationToken);
        var items = await dictItemRepository.Query()
            .Where(item => item.DictTypeId == entity.Id && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        await unitOfWork.ExecuteAsync(async () =>
        {
            if (items.Count > 0)
            {
                foreach (var item in items)
                {
                    item.IsDeleted = true;
                    item.DeletedBy = currentUser.GetAsterErpUserId();
                    item.DeletedTime = DateTime.UtcNow;
                }

                await dictItemRepository.UpdateRangeAsync(items, cancellationToken);
            }

            await dictTypeRepository.DeleteAsync(entity.Id, cancellationToken);
        }, cancellationToken);
    }

    public async Task<DictItemListItemResponse> CreateItemAsync(string dictTypeId, DictItemUpsertRequest request, CancellationToken cancellationToken = default)
    {
        DictDomainPolicy.EnsureItemRequest(request.ItemLabel, request.ItemValue);
        await EnsureTypeExistsAsync(dictTypeId, cancellationToken);
        await EnsureItemValueUniqueAsync(dictTypeId, request.ItemValue, null, cancellationToken);

        var entity = new SystemDictItemEntity
        {
            DictTypeId = dictTypeId,
            ItemLabel = request.ItemLabel.Trim(),
            ItemValue = request.ItemValue.Trim(),
            IsEnabled = request.IsEnabled,
            Remark = request.Remark?.Trim(),
            SortOrder = request.SortOrder
        };

        await dictItemRepository.InsertAsync(entity, cancellationToken);
        return MapItem(entity);
    }

    public async Task<DictItemListItemResponse> UpdateItemAsync(string id, DictItemUpsertRequest request, CancellationToken cancellationToken = default)
    {
        DictDomainPolicy.EnsureItemRequest(request.ItemLabel, request.ItemValue);

        var entity = await EnsureItemExistsAsync(id, cancellationToken);
        await EnsureItemValueUniqueAsync(entity.DictTypeId, request.ItemValue, id, cancellationToken);

        entity.ItemLabel = request.ItemLabel.Trim();
        entity.ItemValue = request.ItemValue.Trim();
        entity.IsEnabled = request.IsEnabled;
        entity.Remark = request.Remark?.Trim();
        entity.SortOrder = request.SortOrder;

        await dictItemRepository.UpdateAsync(entity, cancellationToken);
        return MapItem(entity);
    }

    public async Task DeleteItemAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await EnsureItemExistsAsync(id, cancellationToken);
        await dictItemRepository.DeleteAsync(entity.Id, cancellationToken);
    }

    private async Task<SystemDictTypeEntity> EnsureTypeExistsAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await dictTypeRepository.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        return entity ?? throw new NotFoundException("字典类型不存在", ErrorCodes.DictTypeNotFound);
    }

    private async Task<SystemDictItemEntity> EnsureItemExistsAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await dictItemRepository.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        return entity ?? throw new NotFoundException("字典明细不存在", ErrorCodes.DictItemNotFound);
    }

    private async Task EnsureTypeCodeUniqueAsync(string dictCode, string? currentId, CancellationToken cancellationToken)
    {
        var normalizedCode = dictCode.Trim();
        var exists = await dictTypeRepository.ExistsAsync(
            item => item.DictCode == normalizedCode && item.Id != (currentId ?? string.Empty) && !item.IsDeleted,
            cancellationToken);

        if (exists)
        {
            throw new ValidationException("字典编码已存在", ErrorCodes.DuplicateDictCode);
        }
    }

    private async Task EnsureItemValueUniqueAsync(string dictTypeId, string itemValue, string? currentId, CancellationToken cancellationToken)
    {
        var normalizedValue = itemValue.Trim();
        var exists = await dictItemRepository.ExistsAsync(
            item => item.DictTypeId == dictTypeId && item.ItemValue == normalizedValue && item.Id != (currentId ?? string.Empty) && !item.IsDeleted,
            cancellationToken);

        if (exists)
        {
            throw new ValidationException("字典值已存在", ErrorCodes.DuplicateDictItemValue);
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static DictTypeListItemResponse MapType(SystemDictTypeEntity entity)
    {
        return new DictTypeListItemResponse(entity.Id, entity.DictCode, entity.DictName, entity.IsEnabled, entity.Remark);
    }

    private static DictItemListItemResponse MapItem(SystemDictItemEntity entity)
    {
        return new DictItemListItemResponse(entity.Id, entity.DictTypeId, entity.ItemLabel, entity.ItemValue, entity.SortOrder, entity.IsEnabled, entity.Remark);
    }

    private static ISugarQueryable<SystemDictTypeEntity> ApplyDefaultTypeSort(ISugarQueryable<SystemDictTypeEntity> query) =>
        query.OrderBy(item => item.CreatedTime, OrderByType.Desc);

    private static ISugarQueryable<SystemDictItemEntity> ApplyDefaultItemSort(ISugarQueryable<SystemDictItemEntity> query) =>
        query.OrderBy(item => item.SortOrder, OrderByType.Asc)
            .OrderBy(item => item.CreatedTime, OrderByType.Asc);
}
