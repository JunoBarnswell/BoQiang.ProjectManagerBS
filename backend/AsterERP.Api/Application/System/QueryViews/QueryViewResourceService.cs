using System.Reflection;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.System.QueryViews;
using AsterERP.Api.Modules.System.Dicts;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.QueryViews;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.Users;
using SqlSugar;

namespace AsterERP.Api.Application.System.QueryViews;

public sealed class QueryViewResourceService(IWorkspaceDatabaseAccessor databaseAccessor) : IQueryViewResourceService
{
    private static readonly IReadOnlyList<TableResourceDefinition> AllowedTables =
    [
        new("system_user", "用户表", "system", typeof(SystemUserEntity)),
        new("system_dept", "部门表", "system", typeof(SystemDepartmentEntity)),
        new("system_position", "岗位表", "system", typeof(SystemPositionEntity)),
        new("system_menu", "菜单表", "system", typeof(SystemMenuEntity)),
        new("system_role", "角色表", "system", typeof(SystemRoleEntity)),
        new("system_dict_type", "字典类型表", "system", typeof(SystemDictTypeEntity)),
        new("system_dict_item", "字典项表", "system", typeof(SystemDictItemEntity)),
        new("system_user_role", "用户角色表", "system", typeof(SystemUserRoleEntity))
    ];

    public async Task<QueryViewResourceSyncResponse> SyncAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var tableCount = 0;
        var columnCount = 0;

        foreach (var item in AllowedTables)
        {
            var tableName = ResolveTableName(item.EntityType);
            var table = await GetOrCreateTableAsync(item.Code, tableName, item.Name, item.Module, now, cancellationToken);
            tableCount++;
            var columns = ReadColumns(item.EntityType);
            foreach (var column in columns)
            {
                await UpsertColumnAsync(table.Id, column, now, cancellationToken);
                columnCount++;
            }
        }

        return new QueryViewResourceSyncResponse(tableCount, columnCount, now);
    }

    public async Task<IReadOnlyList<QueryViewTableResourceResponse>> GetTablesAsync(CancellationToken cancellationToken = default)
    {
        var tables = await databaseAccessor.GetCurrentDb().Queryable<SystemQueryViewTableResourceEntity>()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.ModuleCode)
            .OrderBy(item => item.TableCode)
            .ToListAsync(cancellationToken);
        var tableIds = tables.Select(item => item.Id).ToList();
        var columns = tableIds.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<SystemQueryViewColumnResourceEntity>()
                .Where(item => tableIds.Contains(item.TableResourceId) && !item.IsDeleted)
                .OrderBy(item => item.SortOrder)
                .ToListAsync(cancellationToken);
        var columnsByTable = columns.GroupBy(item => item.TableResourceId)
            .ToDictionary(group => group.Key, group => group.Select(MapColumn).ToList());

        return tables.Select(table => new QueryViewTableResourceResponse(
            table.Id,
            table.TableCode,
            table.TableName,
            table.TableComment,
            table.SchemaName,
            table.ModuleCode,
            table.IsEnabled,
            columnsByTable.TryGetValue(table.Id, out var tableColumns) ? tableColumns : []))
            .ToList();
    }

    public async Task SetTableEnabledAsync(string id, bool enabled, CancellationToken cancellationToken = default)
    {
        var table = await GetRequiredTableAsync(id, cancellationToken);
        table.IsEnabled = enabled;
        table.UpdatedTime = DateTime.UtcNow;
        await databaseAccessor.GetCurrentDb().Updateable(table)
            .UpdateColumns(item => new { item.IsEnabled, item.UpdatedTime })
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task SetColumnEnabledAsync(string id, bool enabled, CancellationToken cancellationToken = default)
    {
        var column = await GetRequiredColumnAsync(id, cancellationToken);
        column.IsEnabled = enabled;
        column.UpdatedTime = DateTime.UtcNow;
        await databaseAccessor.GetCurrentDb().Updateable(column)
            .UpdateColumns(item => new { item.IsEnabled, item.UpdatedTime })
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task<SystemQueryViewTableResourceEntity> GetOrCreateTableAsync(
        string tableCode,
        string tableName,
        string tableComment,
        string moduleCode,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = (await databaseAccessor.GetCurrentDb().Queryable<SystemQueryViewTableResourceEntity>()
            .Where(item => item.TableCode == tableCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        if (existing is not null)
        {
            existing.TableName = tableName;
            existing.TableComment = tableComment;
            existing.ModuleCode = moduleCode;
            existing.UpdatedTime = now;
            await databaseAccessor.GetCurrentDb().Updateable(existing).ExecuteCommandAsync(cancellationToken);
            return existing;
        }

        var entity = new SystemQueryViewTableResourceEntity
        {
            TableCode = tableCode,
            TableName = tableName,
            TableComment = tableComment,
            SchemaName = string.Empty,
            ModuleCode = moduleCode,
            CreatedTime = now
        };

        await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity;
    }

    private async Task UpsertColumnAsync(
        string tableResourceId,
        ColumnSnapshot column,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = (await databaseAccessor.GetCurrentDb().Queryable<SystemQueryViewColumnResourceEntity>()
            .Where(item => item.TableResourceId == tableResourceId && item.ColumnCode == column.ColumnName && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        if (existing is not null)
        {
            existing.ColumnName = column.ColumnName;
            existing.ColumnComment = column.ColumnName;
            existing.DataType = column.DataType;
            existing.IsPrimaryKey = column.IsPrimaryKey;
            existing.IsNullable = column.IsNullable;
            existing.SortOrder = column.SortOrder;
            existing.UpdatedTime = now;
            await databaseAccessor.GetCurrentDb().Updateable(existing).ExecuteCommandAsync(cancellationToken);
            return;
        }

        var entity = new SystemQueryViewColumnResourceEntity
        {
            TableResourceId = tableResourceId,
            ColumnCode = column.ColumnName,
            ColumnName = column.ColumnName,
            ColumnComment = column.ColumnName,
            DataType = column.DataType,
            IsPrimaryKey = column.IsPrimaryKey,
            IsNullable = column.IsNullable,
            IsEnabled = true,
            SortOrder = column.SortOrder,
            CreatedTime = now
        };
        await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    private static string ResolveTableName(Type entityType)
    {
        return entityType.GetCustomAttribute<SugarTable>()?.TableName ?? entityType.Name;
    }

    private static List<ColumnSnapshot> ReadColumns(Type entityType)
    {
        return entityType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetCustomAttribute<SugarColumn>()?.IsIgnore != true)
            .Select((property, index) =>
            {
                var column = property.GetCustomAttribute<SugarColumn>();
                var columnName = string.IsNullOrWhiteSpace(column?.ColumnName) ? property.Name : column.ColumnName;
                return new ColumnSnapshot(
                    columnName,
                    NormalizeDataType(property.PropertyType),
                    column?.IsPrimaryKey == true,
                    column?.IsNullable == true || IsNullableProperty(property),
                    index + 1);
            })
            .ToList();
    }

    private static bool IsNullableProperty(PropertyInfo property)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        return !type.IsValueType || Nullable.GetUnderlyingType(property.PropertyType) is not null;
    }

    private static string NormalizeDataType(Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (type == typeof(bool))
        {
            return "bool";
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(decimal) || type == typeof(double) || type == typeof(float))
        {
            return "number";
        }

        if (type == typeof(DateTime))
        {
            return "date";
        }

        return "string";
    }

    private async Task<SystemQueryViewTableResourceEntity> GetRequiredTableAsync(string id, CancellationToken cancellationToken)
    {
        var table = (await databaseAccessor.GetCurrentDb().Queryable<SystemQueryViewTableResourceEntity>()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        return table ?? throw new ValidationException("表资源不存在");
    }

    private async Task<SystemQueryViewColumnResourceEntity> GetRequiredColumnAsync(string id, CancellationToken cancellationToken)
    {
        var column = (await databaseAccessor.GetCurrentDb().Queryable<SystemQueryViewColumnResourceEntity>()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        return column ?? throw new ValidationException("字段资源不存在");
    }

    private static QueryViewColumnResourceResponse MapColumn(SystemQueryViewColumnResourceEntity column)
    {
        return new QueryViewColumnResourceResponse(
            column.Id,
            column.TableResourceId,
            column.ColumnCode,
            column.ColumnName,
            column.ColumnComment,
            column.DataType,
            column.IsPrimaryKey,
            column.IsNullable,
            column.IsEnabled,
            column.SortOrder);
    }

    private sealed record ColumnSnapshot(
        string ColumnName,
        string DataType,
        bool IsPrimaryKey,
        bool IsNullable,
        int SortOrder);

    private sealed record TableResourceDefinition(
        string Code,
        string Name,
        string Module,
        Type EntityType);
}

