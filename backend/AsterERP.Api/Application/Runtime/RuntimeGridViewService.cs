using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using System.Text.RegularExpressions;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.Runtime;
using AsterERP.Api.Modules.Runtime;
using SqlSugar;

namespace AsterERP.Api.Application.Runtime;

public sealed class RuntimeGridViewService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IRuntimePageSchemaService pageSchemaService) : IRuntimeGridViewService
{
    private const int MaxViewJsonLength = 262144;
    private const int MaxColumnDepth = 3;
    private static readonly JsonSerializerOptions ViewJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex TemplateTokenRegex = new(@"\{(?<field>[A-Za-z][A-Za-z0-9_.:-]*)\}", RegexOptions.Compiled);

    public async Task<RuntimeGridViewResponse> GetAsync(
        string pageCode,
        string? previewPageId = null,
        CancellationToken cancellationToken = default)
    {
        var page = await pageSchemaService.GetPublishedPageAsync(pageCode, previewPageId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(previewPageId))
        {
            return new RuntimeGridViewResponse(
                page.PageCode,
                "schema",
                ParseSchemaColumns(page.PageCode, page.ArtifactJson),
                null,
                null);
        }

        var tenantView = await GetTenantViewEntityAsync(page.PageCode, cancellationToken);
        var userView = await GetUserViewEntityAsync(page.PageCode, cancellationToken);
        var (source, columns) = ResolveColumns(page.PageCode, page.ArtifactJson, tenantView?.ViewJson, userView?.ViewJson);

        return new RuntimeGridViewResponse(
            page.PageCode,
            source,
            columns,
            tenantView?.ViewJson,
            userView?.ViewJson);
    }

    public async Task<RuntimeGridViewResponse> SaveUserViewAsync(
        string pageCode,
        RuntimeGridViewSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = await pageSchemaService.GetPublishedPageAsync(pageCode, null, cancellationToken);
        await ValidateColumnsForPageAsync(page.ModelCode, request.Columns, cancellationToken);
        var viewJson = SerializeColumns(request.Columns);
        var existing = await GetUserViewEntityAsync(page.PageCode, cancellationToken);

        if (existing is null)
        {
            var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
            await db.Insertable(new SystemUserGridViewEntity
            {
                UserId = currentUser.GetAsterErpUserId(),
                TenantId = currentUser.GetAsterErpTenantId() ?? string.Empty,
                AppCode = (currentUser.GetAsterErpAppCode() ?? string.Empty).Trim().ToUpperInvariant(),
                PageCode = page.PageCode,
                ViewJson = viewJson,
                VersionNo = 1
            }).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            existing.ViewJson = viewJson;
            existing.VersionNo += 1;
            existing.UpdatedTime = DateTime.UtcNow;
            existing.IsDeleted = false;
            var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
            await db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
        }

        return await GetAsync(page.PageCode, null, cancellationToken);
    }

    public async Task<RuntimeGridViewResponse> SaveTenantDefaultAsync(
        string pageCode,
        RuntimeGridViewSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCanSaveTenantDefault();
        var page = await pageSchemaService.GetPublishedPageAsync(pageCode, null, cancellationToken);
        await ValidateColumnsForPageAsync(page.ModelCode, request.Columns, cancellationToken);
        var viewJson = SerializeColumns(request.Columns);
        var existing = await GetTenantViewEntityAsync(page.PageCode, cancellationToken);

        if (existing is null)
        {
            var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
            await db.Insertable(new SystemTenantGridViewEntity
            {
                TenantId = currentUser.GetAsterErpTenantId() ?? string.Empty,
                AppCode = (currentUser.GetAsterErpAppCode() ?? string.Empty).Trim().ToUpperInvariant(),
                PageCode = page.PageCode,
                ViewJson = viewJson,
                VersionNo = 1
            }).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            existing.ViewJson = viewJson;
            existing.VersionNo += 1;
            existing.UpdatedTime = DateTime.UtcNow;
            existing.IsDeleted = false;
            var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
            await db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
        }

        return await GetAsync(page.PageCode, null, cancellationToken);
    }

    public async Task<RuntimeGridViewResponse> ResetUserViewAsync(string pageCode, CancellationToken cancellationToken = default)
    {
        var page = await pageSchemaService.GetPublishedPageAsync(pageCode, null, cancellationToken);
        var existing = await GetUserViewEntityAsync(page.PageCode, cancellationToken);
        if (existing is not null)
        {
            existing.IsDeleted = true;
            existing.DeletedTime = DateTime.UtcNow;
            existing.UpdatedTime = DateTime.UtcNow;
            var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
            await db.Updateable(existing)
                .UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime })
                .ExecuteCommandAsync(cancellationToken);
        }

        return await GetAsync(page.PageCode, null, cancellationToken);
    }

    private async Task<SystemTenantGridViewEntity?> GetTenantViewEntityAsync(string pageCode, CancellationToken cancellationToken) =>
        (await (await databaseAccessor.RequireApplicationDbAsync(cancellationToken)).Queryable<SystemTenantGridViewEntity>()
            .Where(item => !item.IsDeleted && item.PageCode == pageCode)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

    private async Task<SystemUserGridViewEntity?> GetUserViewEntityAsync(string pageCode, CancellationToken cancellationToken)
    {
        var currentUserId = currentUser.GetAsterErpUserId();
        return (await (await databaseAccessor.RequireApplicationDbAsync(cancellationToken)).Queryable<SystemUserGridViewEntity>()
            .Where(item => !item.IsDeleted && item.UserId == currentUserId && item.PageCode == pageCode)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
    }

    private static (string Source, IReadOnlyList<RuntimeGridViewColumnResponse> Columns) ResolveColumns(
        string pageCode,
        string schemaJson,
        string? tenantViewJson,
        string? userViewJson)
    {
        if (!string.IsNullOrWhiteSpace(userViewJson))
        {
            return ("user", ParseColumns(userViewJson, "用户个人视图配置无效"));
        }

        if (!string.IsNullOrWhiteSpace(tenantViewJson))
        {
            return ("tenant", ParseColumns(tenantViewJson, "租户默认视图配置无效"));
        }

        return ("schema", ParseSchemaColumns(pageCode, schemaJson));
    }

    private static IReadOnlyList<RuntimeGridViewColumnResponse> ParseSchemaColumns(string pageCode, string schemaJson)
    {
        using var document = JsonDocument.Parse(schemaJson);
        if (!document.RootElement.TryGetProperty("grid", out var grid) ||
            !grid.TryGetProperty("columns", out var columns) ||
            columns.ValueKind != JsonValueKind.Array)
        {
            throw new ValidationException($"运行时页面未配置 grid.columns: {pageCode}", ErrorCodes.RuntimeGridViewInvalid);
        }

        return ParseColumnArray(columns);
    }

    private static IReadOnlyList<RuntimeGridViewColumnResponse> ParseColumns(string viewJson, string message)
    {
        if (viewJson.Length > MaxViewJsonLength)
        {
            throw new ValidationException(message, ErrorCodes.RuntimeGridViewInvalid);
        }

        using var document = JsonDocument.Parse(viewJson);
        if (!document.RootElement.TryGetProperty("columns", out var columns) ||
            columns.ValueKind != JsonValueKind.Array)
        {
            throw new ValidationException(message, ErrorCodes.RuntimeGridViewInvalid);
        }

        return ParseColumnArray(columns);
    }

    private static IReadOnlyList<RuntimeGridViewColumnResponse> ParseColumnArray(JsonElement columns)
    {
        var result = new List<RuntimeGridViewColumnResponse>();
        foreach (var column in columns.EnumerateArray())
        {
            var key = ReadString(column, "key");
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ValidationException("列配置 key 不能为空", ErrorCodes.RuntimeGridViewInvalid);
            }

            result.Add(new RuntimeGridViewColumnResponse(
                key,
                ReadString(column, "title"),
                ReadString(column, "binding"),
                ReadString(column, "width"),
                ReadString(column, "fixed"),
                ReadBool(column, "isVisible"),
                ReadInt(column, "order"),
                ReadString(column, "renderer"),
                ReadString(column, "queryField"),
                ReadString(column, "sortField"),
                ReadValueSource(column),
                ReadMerge(column),
                ReadChildren(column)));
        }

        return result
            .OrderBy(item => item.Order ?? 0)
            .ToList();
    }

    private static string SerializeColumns(IReadOnlyList<RuntimeGridViewColumnResponse> columns)
    {
        if (columns.Count == 0)
        {
            throw new ValidationException("列配置不能为空", ErrorCodes.RuntimeGridViewInvalid);
        }

        var viewJson = JsonSerializer.Serialize(new { columns }, ViewJsonOptions);
        if (viewJson.Length > MaxViewJsonLength)
        {
            throw new ValidationException("列配置过大", ErrorCodes.RuntimeGridViewInvalid);
        }

        return viewJson;
    }

    private async Task ValidateColumnsForPageAsync(
        string? modelCode,
        IReadOnlyList<RuntimeGridViewColumnResponse> columns,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(modelCode))
        {
            ValidateColumnTree(columns, new HashSet<string>(StringComparer.OrdinalIgnoreCase), null, 1);
            return;
        }

        var model = (await (await databaseAccessor.RequireApplicationDbAsync(cancellationToken)).Queryable<SystemDataModelEntity>()
            .Where(item => !item.IsDeleted && item.ModelCode == modelCode && item.Status == "Published")
            .OrderBy(item => item.VersionNo, OrderByType.Desc)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new ValidationException("运行时列视图关联的数据模型不存在", ErrorCodes.RuntimeDataModelNotFound);

        RuntimeDataModelSchema? schema;
        try
        {
            schema = JsonSerializer.Deserialize<RuntimeDataModelSchema>(model.SchemaJson, ViewJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"运行时数据模型配置不是合法 JSON: {ex.Message}", ErrorCodes.RuntimeDataModelInvalid);
        }

        var fieldMap = schema?.Fields?.ToDictionary(item => item.FieldCode, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, RuntimeDataFieldDefinition>(StringComparer.OrdinalIgnoreCase);
        if (fieldMap.Count == 0)
        {
            throw new ValidationException("运行时数据模型未配置字段", ErrorCodes.RuntimeDataModelInvalid);
        }

        ValidateColumnTree(columns, new HashSet<string>(StringComparer.OrdinalIgnoreCase), fieldMap, 1);
    }

    private static void ValidateColumnTree(
        IReadOnlyList<RuntimeGridViewColumnResponse> columns,
        HashSet<string> seenKeys,
        IReadOnlyDictionary<string, RuntimeDataFieldDefinition>? fieldMap,
        int depth)
    {
        if (depth > MaxColumnDepth)
        {
            throw new ValidationException($"列层级不能超过 {MaxColumnDepth} 层", ErrorCodes.RuntimeGridViewInvalid);
        }

        foreach (var column in columns)
        {
            if (string.IsNullOrWhiteSpace(column.Key))
            {
                throw new ValidationException("列配置 key 不能为空", ErrorCodes.RuntimeGridViewInvalid);
            }

            if (!seenKeys.Add(column.Key))
            {
                throw new ValidationException($"列配置 key 重复: {column.Key}", ErrorCodes.RuntimeGridViewInvalid);
            }

            if (column.Children is { Count: > 0 })
            {
                ValidateColumnTree(column.Children, seenKeys, fieldMap, depth + 1);
                continue;
            }

            ValidateFieldReference(column.Binding, fieldMap, $"列 {column.Key} 的 binding 不在字段白名单内", requireQueryable: false, requireSortable: false);
            if (fieldMap is not null &&
                string.IsNullOrWhiteSpace(column.Binding) &&
                column.ValueSource is null &&
                !fieldMap.ContainsKey(column.Key))
            {
                throw new ValidationException($"列 {column.Key} 必须配置 binding 或 valueSource", ErrorCodes.RuntimeFieldNotAllowed);
            }
            ValidateFieldReference(column.QueryField, fieldMap, $"列 {column.Key} 的 queryField 不允许查询", requireQueryable: true, requireSortable: false);
            ValidateFieldReference(column.SortField, fieldMap, $"列 {column.Key} 的 sortField 不允许排序", requireQueryable: false, requireSortable: true);
            ValidateValueSource(column, fieldMap);
            ValidateMerge(column, fieldMap);
        }
    }

    private static void ValidateValueSource(
        RuntimeGridViewColumnResponse column,
        IReadOnlyDictionary<string, RuntimeDataFieldDefinition>? fieldMap)
    {
        if (column.ValueSource is null)
        {
            return;
        }

        var sourceType = column.ValueSource.Type.Trim().ToLowerInvariant();
        if (sourceType is not ("field" or "template" or "concat" or "nested"))
        {
            throw new ValidationException($"列 {column.Key} 的 valueSource 类型不支持", ErrorCodes.RuntimeGridViewInvalid);
        }

        if (sourceType == "field")
        {
            ValidateFieldReference(column.ValueSource.Field, fieldMap, $"列 {column.Key} 的 valueSource.field 不在字段白名单内", false, false);
            return;
        }

        if (sourceType == "template")
        {
            if (string.IsNullOrWhiteSpace(column.ValueSource.Template))
            {
                throw new ValidationException($"列 {column.Key} 的模板不能为空", ErrorCodes.RuntimeGridViewInvalid);
            }

            foreach (Match match in TemplateTokenRegex.Matches(column.ValueSource.Template))
            {
                ValidateFieldReference(match.Groups["field"].Value, fieldMap, $"列 {column.Key} 的模板字段不在字段白名单内", false, false);
            }
            return;
        }

        foreach (var field in column.ValueSource.Fields ?? [])
        {
            ValidateFieldReference(field, fieldMap, $"列 {column.Key} 的 valueSource.fields 不在字段白名单内", false, false);
        }
    }

    private static void ValidateMerge(
        RuntimeGridViewColumnResponse column,
        IReadOnlyDictionary<string, RuntimeDataFieldDefinition>? fieldMap)
    {
        if (column.Merge is null || !column.Merge.Enabled)
        {
            return;
        }

        if (column.Merge.Direction is not ("vertical" or "horizontal"))
        {
            throw new ValidationException($"列 {column.Key} 的合并方向无效", ErrorCodes.RuntimeGridViewInvalid);
        }

        if (column.Merge.Strategy is not ("same-value" or "empty-following"))
        {
            throw new ValidationException($"列 {column.Key} 的合并策略无效", ErrorCodes.RuntimeGridViewInvalid);
        }

        foreach (var field in column.Merge.Fields)
        {
            ValidateFieldReference(field, fieldMap, $"列 {column.Key} 的合并字段不在字段白名单内", false, false);
        }
    }

    private static void ValidateFieldReference(
        string? fieldCode,
        IReadOnlyDictionary<string, RuntimeDataFieldDefinition>? fieldMap,
        string message,
        bool requireQueryable,
        bool requireSortable)
    {
        if (string.IsNullOrWhiteSpace(fieldCode) || fieldMap is null)
        {
            return;
        }

        if (!fieldMap.TryGetValue(fieldCode.Trim(), out var field))
        {
            throw new ValidationException(message, ErrorCodes.RuntimeFieldNotAllowed);
        }

        if (requireQueryable && !field.Queryable)
        {
            throw new ValidationException(message, ErrorCodes.RuntimeFieldNotAllowed);
        }

        if (requireSortable && !field.Sortable)
        {
            throw new ValidationException(message, ErrorCodes.RuntimeFieldNotAllowed);
        }
    }

    private void EnsureCanSaveTenantDefault()
    {
        if (currentUser.IsAsterErpTenantAdmin() ||
            currentUser.IsAsterErpPlatformAdmin() ||
            currentUser.HasAsterErpPermission("*"))
        {
            return;
        }

        throw new ValidationException("无权限保存租户默认视图", ErrorCodes.PermissionDenied);
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? ReadBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;

    private static int? ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private static RuntimeGridValueSourceResponse? ReadValueSource(JsonElement element)
    {
        if (!element.TryGetProperty("valueSource", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new RuntimeGridValueSourceResponse(
            ReadString(value, "type") ?? "field",
            ReadString(value, "field"),
            ReadString(value, "template"),
            ReadStringArray(value, "fields"),
            ReadString(value, "path"));
    }

    private static RuntimeGridMergeResponse? ReadMerge(JsonElement element)
    {
        if (!element.TryGetProperty("merge", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new RuntimeGridMergeResponse(
            ReadBool(value, "enabled") ?? false,
            ReadString(value, "direction") ?? "vertical",
            ReadString(value, "strategy") ?? "same-value",
            ReadStringArray(value, "fields") ?? []);
    }

    private static IReadOnlyList<RuntimeGridViewColumnResponse>? ReadChildren(JsonElement element)
    {
        if (!element.TryGetProperty("children", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return ParseColumnArray(value);
    }

    private static IReadOnlyList<string>? ReadStringArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return value
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToList();
    }
}

