using AsterERP.Api.Modules.System.Menus;
using AsterERP.Api.Infrastructure.Database;
using SqlSugar;

namespace AsterERP.Api.Application.Runtime;

public sealed class SystemMenuDataModelProvider(IWorkspaceDatabaseAccessor databaseAccessor) : IDataModelProvider
{
    public string ProviderKey => "system.menus";

    public async Task<RuntimeDataModelQueryResult> QueryAsync(
        RuntimeDataModelDefinition model,
        RuntimeDataModelQuery query,
        CancellationToken cancellationToken = default)
    {
        var dbQuery = databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>().Where(item => !item.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword;
            dbQuery = dbQuery.Where(item =>
                item.MenuName.Contains(keyword) ||
                item.MenuCode.Contains(keyword) ||
                (item.PageCode != null && item.PageCode.Contains(keyword)) ||
                (item.RoutePath != null && item.RoutePath.Contains(keyword)));
        }

        foreach (var filter in query.Filters)
        {
            dbQuery = ApplyFilter(dbQuery, filter);
        }

        dbQuery = ApplySorts(dbQuery, query.Sorts);
        var total = await dbQuery.CountAsync(cancellationToken);
        var entities = await dbQuery
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new RuntimeDataModelQueryResult(
            entities.Select(item => MapRow(item, model.Fields)).ToList(),
            total);
    }

    public async Task<IReadOnlyDictionary<string, object?>?> GetDetailAsync(
        RuntimeDataModelDefinition model,
        string id,
        CancellationToken cancellationToken = default)
    {
        var entity = (await databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>()
            .Where(item => !item.IsDeleted && item.Id == id)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        return entity is null ? null : MapRow(entity, model.Fields);
    }

    public Task<bool> UpdateFieldsAsync(
        RuntimeDataModelDefinition model,
        string id,
        IReadOnlyList<RuntimeDataModelFieldUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<IReadOnlyDictionary<string, object?>?> CreateAsync(
        RuntimeDataModelDefinition model,
        IReadOnlyList<RuntimeDataModelFieldUpdate> values,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyDictionary<string, object?>?>(null);
    }

    public Task<bool> DeleteAsync(
        RuntimeDataModelDefinition model,
        string id,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    private static ISugarQueryable<SystemMenuEntity> ApplyFilter(ISugarQueryable<SystemMenuEntity> query, RuntimeDataModelFilter filter)
    {
        var value = RuntimeDataProviderSupport.CoerceValue(filter.Value, filter.Field.DataType);
        return filter.Field.Binding switch
        {
            "id" => ApplyIdFilter(query, filter.Operator, value?.ToString()),
            "menuName" => ApplyMenuNameFilter(query, filter.Operator, value?.ToString()),
            "menuCode" => ApplyMenuCodeFilter(query, filter.Operator, value?.ToString()),
            "parentCode" => ApplyParentCodeFilter(query, filter.Operator, value?.ToString()),
            "routePath" => ApplyRoutePathFilter(query, filter.Operator, value?.ToString()),
            "pageCode" => ApplyPageCodeFilter(query, filter.Operator, value?.ToString()),
            "menuType" => ApplyMenuTypeFilter(query, filter.Operator, value?.ToString()),
            "permissionCode" => ApplyPermissionCodeFilter(query, filter.Operator, value?.ToString()),
            "icon" => ApplyIconFilter(query, filter.Operator, value?.ToString()),
            "sortOrder" => ApplySortOrderFilter(query, filter),
            "visible" => ApplyVisibleFilter(query, value),
            "createdTime" => ApplyCreatedTimeFilter(query, filter),
            "updatedTime" => ApplyUpdatedTimeFilter(query, filter),
            _ => query
        };
    }

    private static ISugarQueryable<SystemMenuEntity> ApplySorts(ISugarQueryable<SystemMenuEntity> query, IReadOnlyList<RuntimeDataModelSort> sorts)
    {
        if (sorts.Count == 0)
        {
            return query.OrderBy(item => item.SortOrder, OrderByType.Asc).OrderBy(item => item.CreatedTime, OrderByType.Asc);
        }

        foreach (var sort in sorts)
        {
            var order = RuntimeDataProviderSupport.ToOrderByType(sort.Order);
            query = sort.Field.Binding switch
            {
                "menuName" => query.OrderBy(item => item.MenuName, order),
                "menuCode" => query.OrderBy(item => item.MenuCode, order),
                "pageCode" => query.OrderBy(item => item.PageCode, order),
                "menuType" => query.OrderBy(item => item.MenuType, order),
                "sortOrder" => query.OrderBy(item => item.SortOrder, order),
                "visible" => query.OrderBy(item => item.Visible, order),
                "createdTime" => query.OrderBy(item => item.CreatedTime, order),
                "updatedTime" => query.OrderBy(item => item.UpdatedTime, order),
                _ => query
            };
        }

        return query;
    }

    private static IReadOnlyDictionary<string, object?> MapRow(SystemMenuEntity entity, IReadOnlyList<RuntimeDataFieldDefinition> fields)
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            row[field.FieldCode] = field.Binding switch
            {
                "id" => entity.Id,
                "menuName" => entity.MenuName,
                "menuCode" => entity.MenuCode,
                "parentCode" => entity.ParentCode,
                "routePath" => entity.RoutePath,
                "pageCode" => entity.PageCode,
                "menuType" => entity.MenuType,
                "sortOrder" => entity.SortOrder,
                "visible" => entity.Visible,
                "permissionCode" => entity.PermissionCode,
                "icon" => entity.Icon,
                "createdTime" => entity.CreatedTime,
                "updatedTime" => entity.UpdatedTime,
                _ => null
            };
        }

        return row;
    }

    private static ISugarQueryable<SystemMenuEntity> ApplyIdFilter(ISugarQueryable<SystemMenuEntity> query, string operatorName, string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? query
            : operatorName == "notEquals"
                ? query.Where(item => item.Id != value)
                : query.Where(item => item.Id == value);

    private static ISugarQueryable<SystemMenuEntity> ApplyMenuNameFilter(ISugarQueryable<SystemMenuEntity> query, string operatorName, string? value) =>
        ApplyRequiredTextFilter(query, operatorName, value, "menuName");

    private static ISugarQueryable<SystemMenuEntity> ApplyMenuCodeFilter(ISugarQueryable<SystemMenuEntity> query, string operatorName, string? value) =>
        ApplyRequiredTextFilter(query, operatorName, value, "menuCode");

    private static ISugarQueryable<SystemMenuEntity> ApplyMenuTypeFilter(ISugarQueryable<SystemMenuEntity> query, string operatorName, string? value) =>
        ApplyRequiredTextFilter(query, operatorName, value, "menuType");

    private static ISugarQueryable<SystemMenuEntity> ApplyParentCodeFilter(ISugarQueryable<SystemMenuEntity> query, string operatorName, string? value) =>
        ApplyNullableTextFilter(query, operatorName, value, "parentCode");

    private static ISugarQueryable<SystemMenuEntity> ApplyRoutePathFilter(ISugarQueryable<SystemMenuEntity> query, string operatorName, string? value) =>
        ApplyNullableTextFilter(query, operatorName, value, "routePath");

    private static ISugarQueryable<SystemMenuEntity> ApplyPageCodeFilter(ISugarQueryable<SystemMenuEntity> query, string operatorName, string? value) =>
        ApplyNullableTextFilter(query, operatorName, value, "pageCode");

    private static ISugarQueryable<SystemMenuEntity> ApplyPermissionCodeFilter(ISugarQueryable<SystemMenuEntity> query, string operatorName, string? value) =>
        ApplyNullableTextFilter(query, operatorName, value, "permissionCode");

    private static ISugarQueryable<SystemMenuEntity> ApplyIconFilter(ISugarQueryable<SystemMenuEntity> query, string operatorName, string? value) =>
        ApplyNullableTextFilter(query, operatorName, value, "icon");

    private static ISugarQueryable<SystemMenuEntity> ApplyRequiredTextFilter(ISugarQueryable<SystemMenuEntity> query, string operatorName, string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return query;
        }

        return field switch
        {
            "menuName" => operatorName switch
            {
                "contains" => query.Where(item => item.MenuName.Contains(value)),
                "startsWith" => query.Where(item => item.MenuName.StartsWith(value)),
                "endsWith" => query.Where(item => item.MenuName.EndsWith(value)),
                "notEquals" => query.Where(item => item.MenuName != value),
                _ => query.Where(item => item.MenuName == value)
            },
            "menuCode" => operatorName switch
            {
                "contains" => query.Where(item => item.MenuCode.Contains(value)),
                "startsWith" => query.Where(item => item.MenuCode.StartsWith(value)),
                "endsWith" => query.Where(item => item.MenuCode.EndsWith(value)),
                "notEquals" => query.Where(item => item.MenuCode != value),
                _ => query.Where(item => item.MenuCode == value)
            },
            "menuType" => operatorName switch
            {
                "contains" => query.Where(item => item.MenuType.Contains(value)),
                "startsWith" => query.Where(item => item.MenuType.StartsWith(value)),
                "endsWith" => query.Where(item => item.MenuType.EndsWith(value)),
                "notEquals" => query.Where(item => item.MenuType != value),
                _ => query.Where(item => item.MenuType == value)
            },
            _ => query
        };
    }

    private static ISugarQueryable<SystemMenuEntity> ApplyNullableTextFilter(ISugarQueryable<SystemMenuEntity> query, string operatorName, string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return query;
        }

        return field switch
        {
            "parentCode" => operatorName switch
            {
                "contains" => query.Where(item => item.ParentCode != null && item.ParentCode.Contains(value)),
                "startsWith" => query.Where(item => item.ParentCode != null && item.ParentCode.StartsWith(value)),
                "endsWith" => query.Where(item => item.ParentCode != null && item.ParentCode.EndsWith(value)),
                "notEquals" => query.Where(item => item.ParentCode != value),
                _ => query.Where(item => item.ParentCode == value)
            },
            "routePath" => operatorName switch
            {
                "contains" => query.Where(item => item.RoutePath != null && item.RoutePath.Contains(value)),
                "startsWith" => query.Where(item => item.RoutePath != null && item.RoutePath.StartsWith(value)),
                "endsWith" => query.Where(item => item.RoutePath != null && item.RoutePath.EndsWith(value)),
                "notEquals" => query.Where(item => item.RoutePath != value),
                _ => query.Where(item => item.RoutePath == value)
            },
            "pageCode" => operatorName switch
            {
                "contains" => query.Where(item => item.PageCode != null && item.PageCode.Contains(value)),
                "startsWith" => query.Where(item => item.PageCode != null && item.PageCode.StartsWith(value)),
                "endsWith" => query.Where(item => item.PageCode != null && item.PageCode.EndsWith(value)),
                "notEquals" => query.Where(item => item.PageCode != value),
                _ => query.Where(item => item.PageCode == value)
            },
            "permissionCode" => operatorName switch
            {
                "contains" => query.Where(item => item.PermissionCode != null && item.PermissionCode.Contains(value)),
                "startsWith" => query.Where(item => item.PermissionCode != null && item.PermissionCode.StartsWith(value)),
                "endsWith" => query.Where(item => item.PermissionCode != null && item.PermissionCode.EndsWith(value)),
                "notEquals" => query.Where(item => item.PermissionCode != value),
                _ => query.Where(item => item.PermissionCode == value)
            },
            "icon" => operatorName switch
            {
                "contains" => query.Where(item => item.Icon != null && item.Icon.Contains(value)),
                "startsWith" => query.Where(item => item.Icon != null && item.Icon.StartsWith(value)),
                "endsWith" => query.Where(item => item.Icon != null && item.Icon.EndsWith(value)),
                "notEquals" => query.Where(item => item.Icon != value),
                _ => query.Where(item => item.Icon == value)
            },
            _ => query
        };
    }

    private static ISugarQueryable<SystemMenuEntity> ApplySortOrderFilter(
        ISugarQueryable<SystemMenuEntity> query,
        RuntimeDataModelFilter filter)
    {
        var value = RuntimeDataProviderSupport.CoerceValue(filter.Value, filter.Field.DataType);
        var valueTo = RuntimeDataProviderSupport.CoerceValue(filter.ValueTo, filter.Field.DataType);
        if (value is not int intValue)
        {
            return query;
        }

        return filter.Operator switch
        {
            "gt" => query.Where(item => item.SortOrder > intValue),
            "gte" => query.Where(item => item.SortOrder >= intValue),
            "lt" => query.Where(item => item.SortOrder < intValue),
            "lte" => query.Where(item => item.SortOrder <= intValue),
            "between" when valueTo is int intValueTo => query.Where(item => item.SortOrder >= intValue && item.SortOrder <= intValueTo),
            "notEquals" => query.Where(item => item.SortOrder != intValue),
            _ => query.Where(item => item.SortOrder == intValue)
        };
    }

    private static ISugarQueryable<SystemMenuEntity> ApplyVisibleFilter(
        ISugarQueryable<SystemMenuEntity> query,
        object? value) =>
        value is bool boolValue ? query.Where(item => item.Visible == boolValue) : query;

    private static ISugarQueryable<SystemMenuEntity> ApplyCreatedTimeFilter(
        ISugarQueryable<SystemMenuEntity> query,
        RuntimeDataModelFilter filter)
    {
        var value = RuntimeDataProviderSupport.CoerceValue(filter.Value, filter.Field.DataType);
        if (value is not DateTime dateValue)
        {
            return query;
        }

        return filter.Operator switch
        {
            "gt" => query.Where(item => item.CreatedTime > dateValue),
            "gte" => query.Where(item => item.CreatedTime >= dateValue),
            "lt" => query.Where(item => item.CreatedTime < dateValue),
            "lte" => query.Where(item => item.CreatedTime <= dateValue),
            "notEquals" => query.Where(item => item.CreatedTime != dateValue),
            _ => query.Where(item => item.CreatedTime == dateValue)
        };
    }

    private static ISugarQueryable<SystemMenuEntity> ApplyUpdatedTimeFilter(
        ISugarQueryable<SystemMenuEntity> query,
        RuntimeDataModelFilter filter)
    {
        var value = RuntimeDataProviderSupport.CoerceValue(filter.Value, filter.Field.DataType);
        if (value is not DateTime dateValue)
        {
            return query;
        }

        return filter.Operator switch
        {
            "gt" => query.Where(item => item.UpdatedTime > dateValue),
            "gte" => query.Where(item => item.UpdatedTime >= dateValue),
            "lt" => query.Where(item => item.UpdatedTime < dateValue),
            "lte" => query.Where(item => item.UpdatedTime <= dateValue),
            "notEquals" => query.Where(item => item.UpdatedTime != dateValue),
            _ => query.Where(item => item.UpdatedTime == dateValue)
        };
    }
}

