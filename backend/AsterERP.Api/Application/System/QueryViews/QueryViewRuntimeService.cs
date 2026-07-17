using System.Reflection;
using AsterERP.Api.Infrastructure.Database;
using System.Diagnostics;
using System.Text.Json;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.System.Files;
using AsterERP.Contracts.System.Dicts;
using AsterERP.Contracts.System.QueryViews;
using AsterERP.Api.Application.System.Dicts;
using AsterERP.Api.Application.System.Files;
using AsterERP.Api.Application.System.Menus;
using AsterERP.Api.Application.System.Organizations;
using AsterERP.Api.Application.System.Roles;
using AsterERP.Api.Application.System.Users;
using AsterERP.Api.Infrastructure.QueryViews;
using AsterERP.Api.Modules.System.QueryViews;
using SqlSugar;

namespace AsterERP.Api.Application.System.QueryViews;

public sealed class QueryViewRuntimeService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ISystemUserService userService,
    ISystemDepartmentService departmentService,
    ISystemPositionService positionService,
    ISystemMenuService menuService,
    ISystemRoleService roleService,
    IFileAppService fileAppService,
    IDictManagementService dictService,
    IConfiguration configuration,
    ILogger<QueryViewRuntimeService> logger) : IQueryViewRuntimeService
{
    public async Task<QueryViewRuntimeDefinitionResponse> GetDefinitionAsync(string viewCode, CancellationToken cancellationToken = default)
    {
        var definition = await GetPublishedDefinitionAsync(viewCode, cancellationToken);
        var design = QueryViewDesignJson.Deserialize(definition.DesignJson);
        return new QueryViewRuntimeDefinitionResponse(
            definition.ViewCode,
            definition.ViewName,
            definition.ViewType,
            definition.DefaultPageSize,
            definition.MaxPageSize,
            design.Projections.Select(MapField).ToList(),
            design.Conditions,
            design.Sorts);
    }

    public async Task<QueryViewQueryResponse> QueryAsync(string viewCode, QueryViewQueryRequest request, CancellationToken cancellationToken = default)
    {
        var definition = await GetPublishedDefinitionAsync(viewCode, cancellationToken);
        var design = QueryViewDesignJson.Deserialize(definition.DesignJson);
        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = NormalizePageSize(request.PageSize, definition);
        var gridQuery = BuildGridQuery(pageIndex, pageSize, request.Conditions, request.Sorts, design.Projections);
        var queryStartedAt = Stopwatch.GetTimestamp();
        var rows = await QueryRowsAsync(definition.ViewCode, gridQuery, cancellationToken);
        LogSlowRuntimeQuery(definition.ViewCode, pageIndex, pageSize, request.Sorts.Count, rows.Total, queryStartedAt);
        return new QueryViewQueryResponse(
            pageIndex,
            pageSize,
            rows.Total,
            ProjectRows(rows.Items, design.Projections));
    }

    private async Task<SystemQueryViewDefinitionEntity> GetPublishedDefinitionAsync(
        string viewCode,
        CancellationToken cancellationToken)
    {
        var normalized = QueryViewNames.NormalizeViewCode(viewCode);
        var definition = (await databaseAccessor.GetCurrentDb().Queryable<SystemQueryViewDefinitionEntity>()
            .Where(item => item.ViewCode == normalized && item.IsEnabled && item.Status == "Published" && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        return definition ?? throw new ValidationException("查询视图不存在或未发布", ErrorCodes.QueryViewNotFound);
    }

    private async Task<RuntimeRows> QueryRowsAsync(string viewCode, GridQuery query, CancellationToken cancellationToken)
    {
        return viewCode switch
        {
            "system_user_default" => await FromGridResultAsync(
                userService.GetPageAsync(query, cancellationToken)),
            "system_dept_tree" => await FromGridResultAsync(
                departmentService.GetPageAsync(query, cancellationToken)),
            "system_position_default" => await FromGridResultAsync(
                positionService.GetPageAsync(query, cancellationToken)),
            "system_menu_tree" => await FromGridResultAsync(
                menuService.GetPageAsync(query, cancellationToken)),
            "system_role_default" => await FromGridResultAsync(
                roleService.GetPageAsync(query, cancellationToken)),
            "system_file_default" => await FromGridResultAsync(
                fileAppService.GetPageAsync(query, cancellationToken)),
            "system_dict_type" => await FromGridResultAsync(
                dictService.GetTypesPageAsync(query, cancellationToken)),
            "system_dict_item" => await QueryDictItemsAsync(query, cancellationToken),
            _ => throw new ValidationException("查询视图没有可用的 ORM 运行时提供器", ErrorCodes.QueryViewInvalid)
        };
    }

    private async Task<RuntimeRows> QueryDictItemsAsync(GridQuery query, CancellationToken cancellationToken)
    {
        return await FromGridResultAsync(dictService.GetItemsPageAsync(query, cancellationToken));
    }

    private static async Task<RuntimeRows> FromGridResultAsync<TItem>(Task<GridPageResult<TItem>> task)
    {
        var page = await task;
        return new RuntimeRows(page.Total, page.Items.Cast<object>().ToList());
    }

    private static GridQuery BuildGridQuery(
        int pageIndex,
        int pageSize,
        IReadOnlyList<QueryViewQueryCondition> conditions,
        IReadOnlyList<QueryViewQuerySort> sorts,
        IReadOnlyList<QueryViewProjectionRequest> projections)
    {
        var projectionByAlias = projections.ToDictionary(
            projection => projection.FieldAlias,
            projection => projection,
            StringComparer.OrdinalIgnoreCase);
        var query = new GridQuery
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
        };

        foreach (var sort in sorts)
        {
            var sortField = sort.Field?.Trim();
            if (string.IsNullOrWhiteSpace(sortField))
            {
                continue;
            }

            if (!projectionByAlias.TryGetValue(sortField, out var projection) || !projection.IsSortable)
            {
                throw new ValidationException($"排序字段不允许: {sortField}", ErrorCodes.QueryViewInvalid);
            }

            query.Sorts.Add(new GridSort
            {
                Field = sortField,
                Order = sort.Direction
            });
        }

        foreach (var condition in conditions)
        {
            var field = condition.Field?.Trim();
            var value = NormalizeString(condition.Value);
            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (string.Equals(field, "__keyword", StringComparison.OrdinalIgnoreCase))
            {
                query.Keyword = value;
                continue;
            }

            switch (field)
            {
                case "deptId":
                    query.DeptId = value;
                    break;
                case "positionId":
                    query.PositionId = value;
                    break;
                case "roleId":
                    query.RoleId = value;
                    break;
                case "tenantId":
                    query.TenantId = value;
                    break;
                case "appCode":
                    query.AppCode = value;
                    break;
                case "parentId":
                case "parentCode":
                case "dictTypeId":
                    query.ParentId = value;
                    break;
                default:
                    if (!projectionByAlias.TryGetValue(field, out var projection) || !projection.IsQueryable)
                    {
                        throw new ValidationException($"查询字段不允许: {field}", ErrorCodes.QueryViewInvalid);
                    }

                    query.Filters.Add(new GridFilter
                    {
                        Field = field,
                        Operator = condition.Operator,
                        Value = condition.Value,
                        ValueTo = condition.ValueTo
                    });
                    break;
            }
        }

        return query;
    }

    private static IReadOnlyList<Dictionary<string, object?>> ProjectRows(
        IReadOnlyList<object> items,
        IReadOnlyList<QueryViewProjectionRequest> projections)
    {
        return items.Select(item =>
        {
            var values = ToDictionary(item);
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var projection in projections)
            {
                values.TryGetValue(projection.FieldAlias, out var value);
                row[projection.FieldAlias] = value;
            }

            return row;
        }).ToList();
    }

    private static Dictionary<string, object?> ToDictionary(object item)
    {
        if (item is Dictionary<string, object?> dictionary)
        {
            return dictionary;
        }

        return item.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ToDictionary(
                property => LowerFirst(property.Name),
                property => NormalizePropertyValue(property.GetValue(item)),
                StringComparer.OrdinalIgnoreCase);
    }

    private static object? NormalizePropertyValue(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.ToString()
            };
        }

        return value;
    }

    private static string LowerFirst(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? value
            : char.ToLowerInvariant(value[0]) + value[1..];
    }

    private static string? NormalizeString(object? value)
    {
        if (value is JsonElement element)
        {
            return NormalizePropertyValue(element)?.ToString();
        }

        return value?.ToString()?.Trim();
    }

    private static QueryViewRuntimeFieldResponse MapField(QueryViewProjectionRequest field)
    {
        return new QueryViewRuntimeFieldResponse(
            field.FieldCode,
            field.DisplayName,
            field.DataType,
            field.Width,
            field.Align,
            field.IsVisible,
            field.IsQueryable,
            field.IsSortable,
            field.IsExportable,
            field.IsFrozen,
            field.DictType,
            field.MaskRule,
            field.PermissionCode);
    }

    private static int NormalizePageSize(int pageSize, SystemQueryViewDefinitionEntity definition)
    {
        var requested = pageSize <= 0 ? definition.DefaultPageSize : pageSize;
        if (requested > definition.MaxPageSize)
        {
            throw new ValidationException($"分页大小不能超过 {definition.MaxPageSize}", ErrorCodes.QueryViewInvalid);
        }

        return requested;
    }

    private void LogSlowRuntimeQuery(
        string viewCode,
        int pageIndex,
        int pageSize,
        int sortCount,
        long total,
        long startedAt)
    {
        var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        var thresholdMs = configuration.GetValue("QueryDiagnostics:RuntimeQueryThresholdMs", 300);
        if (elapsedMs < thresholdMs)
        {
            return;
        }

        logger.LogInformation(
            "QueryView runtime query {ViewCode} page={PageIndex}/{PageSize} sorts={SortCount} total={Total} elapsed={ElapsedMilliseconds}ms",
            viewCode,
            pageIndex,
            pageSize,
            sortCount,
            total,
            Math.Round(elapsedMs, 2));
    }

    private sealed record RuntimeRows(long Total, IReadOnlyList<object> Items);
}

