using System.Text;
using System.Text.Json;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Runtime;

public sealed class ApplicationDataCenterFileDataModelProvider(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    ApplicationDataSourceConnectionFactory connectionFactory,
    ApplicationDataPreviewReader previewReader)
    : IDataModelProvider
{
    public string ProviderKey => "application-data-center.file";

    public async Task<RuntimeDataModelQueryResult> QueryAsync(
        RuntimeDataModelDefinition model,
        RuntimeDataModelQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rows = await LoadRowsAsync(model, cancellationToken);
        rows = ApplyFilters(rows, query.Filters);
        rows = ApplyKeyword(rows, model, query.Keyword);
        rows = ApplySorts(rows, query.Sorts);
        var total = rows.Count;
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 200);
        return new RuntimeDataModelQueryResult(
            rows.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToArray(),
            total);
    }

    public async Task<IReadOnlyDictionary<string, object?>?> GetDetailAsync(
        RuntimeDataModelDefinition model,
        string id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rows = await LoadRowsAsync(model, cancellationToken);
        return rows.FirstOrDefault(item =>
            item.TryGetValue(model.KeyField, out var value) &&
            string.Equals(value?.ToString(), id, StringComparison.OrdinalIgnoreCase));
    }

    public Task<IReadOnlyDictionary<string, object?>?> CreateAsync(
        RuntimeDataModelDefinition model,
        IReadOnlyList<RuntimeDataModelFieldUpdate> values,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new ValidationException($"文件模型 {model.ModelCode} 仅支持查询，不支持新增数据", ErrorCodes.RuntimeDataModelInvalid);
    }

    public Task<bool> UpdateFieldsAsync(
        RuntimeDataModelDefinition model,
        string id,
        IReadOnlyList<RuntimeDataModelFieldUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new ValidationException($"文件模型 {model.ModelCode} 仅支持查询，不支持编辑数据", ErrorCodes.RuntimeDataModelInvalid);
    }

    public Task<bool> DeleteAsync(
        RuntimeDataModelDefinition model,
        string id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new ValidationException($"文件模型 {model.ModelCode} 仅支持查询，不支持删除数据", ErrorCodes.RuntimeDataModelInvalid);
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> LoadRowsAsync(
        RuntimeDataModelDefinition model,
        CancellationToken cancellationToken)
    {
        var source = model.Source ?? throw new ValidationException("运行时模型缺少来源配置", ErrorCodes.RuntimeDataModelInvalid);
        var dataSourceId = ReadString(source, "dataSourceId");
        if (string.IsNullOrWhiteSpace(dataSourceId))
        {
            throw new ValidationException("文件模型缺少来源数据源", ErrorCodes.RuntimeDataModelInvalid);
        }

        var workspace = workspaceResolver.Resolve();
        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var dataSource = (await appDb.Queryable<ApplicationDataSourceEntity>()
            .Where(item =>
                item.Id == dataSourceId &&
                item.TenantId == workspace.TenantId &&
                item.AppCode == workspace.AppCode &&
                !item.IsDeleted &&
                item.Status != ApplicationDataCenterObjectStatus.Disabled)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("模型来源文件数据源不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);

        var options = connectionFactory.Resolve(dataSource);
        var config = ApplicationDataCenterJson.DeserializeDictionary(dataSource.ConfigJson);
        var preview = dataSource.ObjectType switch
        {
            ApplicationDataSourceType.Excel => previewReader.PreviewExcel(
                Required(options.FilePath, "Excel 文件路径不能为空"),
                ReadString(config, "sheetName"),
                ReadInt(config, "headerRow") ?? 1,
                ReadInt(config, "dataStartRow") ?? 2,
                100),
            ApplicationDataSourceType.Csv => previewReader.PreviewCsv(
                Required(options.FilePath, "CSV 文件路径不能为空"),
                ReadString(config, "delimiter") ?? ",",
                ReadBool(config, "firstRowHeader", true),
                ReadInt(config, "dataStartRow") ?? 1,
                ResolveEncoding(ReadString(config, "encoding")),
                100),
            _ => throw new ValidationException("当前文件模型只支持 Excel/CSV 来源", ErrorCodes.RuntimeDataModelInvalid)
        };

        return preview.Rows.Select(row => ProjectRow(row, model.Fields)).ToArray();
    }

    private static IReadOnlyDictionary<string, object?> ProjectRow(
        IReadOnlyDictionary<string, object?> row,
        IReadOnlyList<RuntimeDataFieldDefinition> fields)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            var binding = string.IsNullOrWhiteSpace(field.Binding) ? field.FieldCode : field.Binding;
            result[field.FieldCode] = row.TryGetValue(binding, out var value) ? value : null;
        }

        return result;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> ApplyKeyword(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        RuntimeDataModelDefinition model,
        string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return rows;
        }

        var queryFields = model.Fields
            .Where(item => item.Queryable)
            .Select(item => item.FieldCode)
            .ToArray();
        var normalizedKeyword = keyword.Trim();
        return rows.Where(row => queryFields.Any(field =>
            row.TryGetValue(field, out var value) &&
            value?.ToString()?.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase) == true)).ToArray();
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> ApplyFilters(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        IReadOnlyList<RuntimeDataModelFilter> filters)
    {
        foreach (var filter in filters)
        {
            rows = rows.Where(row => MatchFilter(row, filter)).ToArray();
        }

        return rows;
    }

    private static bool MatchFilter(
        IReadOnlyDictionary<string, object?> row,
        RuntimeDataModelFilter filter)
    {
        row.TryGetValue(filter.Field.FieldCode, out var raw);
        var value = RuntimeDataProviderSupport.CoerceValue(raw, filter.Field.DataType);
        var expected = RuntimeDataProviderSupport.CoerceValue(filter.Value, filter.Field.DataType);
        var comparison = string.Compare(value?.ToString(), expected?.ToString(), StringComparison.OrdinalIgnoreCase);
        return filter.Operator.ToLowerInvariant() switch
        {
            "contains" => value?.ToString()?.Contains(expected?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true,
            "startswith" => value?.ToString()?.StartsWith(expected?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true,
            "endswith" => value?.ToString()?.EndsWith(expected?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true,
            "notequals" => comparison != 0,
            "gt" => comparison > 0,
            "gte" => comparison >= 0,
            "lt" => comparison < 0,
            "lte" => comparison <= 0,
            _ => comparison == 0
        };
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> ApplySorts(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        IReadOnlyList<RuntimeDataModelSort> sorts)
    {
        IOrderedEnumerable<IReadOnlyDictionary<string, object?>>? ordered = null;
        foreach (var sort in sorts)
        {
            ordered = ordered is null
                ? OrderRows(rows, sort)
                : ThenOrderRows(ordered, sort);
        }

        return ordered?.ToArray() ?? rows;
    }

    private static IOrderedEnumerable<IReadOnlyDictionary<string, object?>> OrderRows(
        IEnumerable<IReadOnlyDictionary<string, object?>> rows,
        RuntimeDataModelSort sort) =>
        sort.Order.Equals("desc", StringComparison.OrdinalIgnoreCase)
            ? rows.OrderByDescending(item => ReadComparable(item, sort.Field.FieldCode))
            : rows.OrderBy(item => ReadComparable(item, sort.Field.FieldCode));

    private static IOrderedEnumerable<IReadOnlyDictionary<string, object?>> ThenOrderRows(
        IOrderedEnumerable<IReadOnlyDictionary<string, object?>> rows,
        RuntimeDataModelSort sort) =>
        sort.Order.Equals("desc", StringComparison.OrdinalIgnoreCase)
            ? rows.ThenByDescending(item => ReadComparable(item, sort.Field.FieldCode))
            : rows.ThenBy(item => ReadComparable(item, sort.Field.FieldCode));

    private static string ReadComparable(IReadOnlyDictionary<string, object?> row, string fieldCode) =>
        row.TryGetValue(fieldCode, out var value) ? value?.ToString() ?? string.Empty : string.Empty;

    private static string Required(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(message, ErrorCodes.RuntimeDataModelInvalid);
        }

        return value.Trim();
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> source, string key)
    {
        if (!source.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value is JsonElement element ? element.ToString() : value.ToString();
    }

    private static int? ReadInt(IReadOnlyDictionary<string, object?> source, string key) =>
        int.TryParse(ReadString(source, key), out var parsed) ? parsed : null;

    private static bool ReadBool(IReadOnlyDictionary<string, object?> source, string key, bool defaultValue)
    {
        var value = ReadString(source, key);
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static Encoding ResolveEncoding(string? value) =>
        string.Equals(value?.Trim(), "unicode", StringComparison.OrdinalIgnoreCase)
            ? Encoding.Unicode
            : Encoding.UTF8;
}
