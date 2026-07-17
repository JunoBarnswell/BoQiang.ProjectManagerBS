using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Ai;
using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseCustomMcpServerService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    FlowisePermissionGuard permissionGuard,
    IAiSecretProtector secretProtector,
    IHttpClientFactory httpClientFactory) : IFlowiseCustomMcpServerService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string ResourceType = "custom-mcp-server";
    private const string RedactedValue = "************";

    public async Task<GridPageResult<FlowiseCustomMcpServerDto>> GetPageAsync(FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseToolsView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
        var dbQuery = db.Queryable<FlowiseCustomMcpServerEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.Name.Contains(keyword) || item.ServerUrl.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.WorkspaceId))
        {
            var workspaceId = query.WorkspaceId.Trim();
            dbQuery = dbQuery.Where(item => item.WorkspaceId == workspaceId);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(NormalizePage(query.PageIndex), NormalizeSize(query.PageSize), total);
        return new GridPageResult<FlowiseCustomMcpServerDto> { Total = total.Value, Items = rows.Select(Map).ToList() };
    }

    public async Task<FlowiseCustomMcpServerDto> GetAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseToolsView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
        return Map(await LoadAsync(id, cancellationToken));
    }

    public async Task<FlowiseCustomMcpServerDto> CreateAsync(FlowiseCustomMcpServerUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseToolsCreate, PermissionCodes.FlowiseToolsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var normalized = Normalize(request, requireAuthConfig: true);
        await EnsureUniqueNameAsync(normalized.Name, null, cancellationToken);

        var workspace = workspaceContext.Resolve();
        var entity = new FlowiseCustomMcpServerEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId
        };
        Apply(entity, normalized, preserveExistingAuthConfig: false);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("custom-mcp-server.created", entity.Id, entity.Name, cancellationToken);
        return Map(entity);
    }

    public async Task<FlowiseCustomMcpServerDto> UpdateAsync(string id, FlowiseCustomMcpServerUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseToolsUpdate, PermissionCodes.FlowiseToolsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadAsync(id, cancellationToken);
        var normalized = Normalize(request, requireAuthConfig: false);
        await EnsureUniqueNameAsync(normalized.Name, entity.Id, cancellationToken);

        Apply(entity, normalized, preserveExistingAuthConfig: string.IsNullOrWhiteSpace(request.AuthConfigJson));
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("custom-mcp-server.updated", entity.Id, entity.Name, cancellationToken);
        return Map(entity);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseToolsDelete, PermissionCodes.FlowiseToolsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("custom-mcp-server.deleted", entity.Id, entity.Name, cancellationToken);
        return true;
    }

    public async Task<FlowiseCustomMcpServerAuthorizeResultDto> AuthorizeAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseToolsUpdate, PermissionCodes.FlowiseToolsCreate, PermissionCodes.FlowiseToolsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadAsync(id, cancellationToken);
        try
        {
            var tools = await DiscoverToolsAsync(entity, cancellationToken);
            entity.ToolsJson = JsonSerializer.Serialize(tools, JsonOptions);
            entity.ToolCount = tools.Count;
            entity.Status = "Authorized";
            entity.ErrorMessage = null;
            entity.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteAuditAsync("custom-mcp-server.authorized", entity.Id, entity.Name, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or ValidationException)
        {
            entity.ToolsJson = "[]";
            entity.ToolCount = 0;
            entity.Status = "Error";
            entity.ErrorMessage = ex.Message;
            entity.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteAuditAsync("custom-mcp-server.authorize-failed", entity.Id, ex.Message, cancellationToken);
        }

        return new FlowiseCustomMcpServerAuthorizeResultDto
        {
            Id = entity.Id,
            Status = entity.Status,
            ToolCount = entity.ToolCount,
            ToolsJson = entity.ToolsJson,
            ErrorMessage = entity.ErrorMessage
        };
    }

    public async Task<IReadOnlyList<FlowiseCustomMcpServerToolDto>> GetToolsAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseToolsView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
        var entity = await LoadAsync(id, cancellationToken);
        return ParseStoredTools(entity.ToolsJson);
    }

    private async Task<IReadOnlyList<FlowiseCustomMcpServerToolDto>> DiscoverToolsAsync(FlowiseCustomMcpServerEntity entity, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, entity.ServerUrl)
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":"tools-list","method":"tools/list","params":{}}""",
                Encoding.UTF8,
                "application/json")
        };
        AddAuthHeaders(request.Headers, entity);
        using var httpClient = httpClientFactory.CreateClient();
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"MCP Server returned {(int)response.StatusCode}: {body}");
        }

        return ParseToolsListResponse(body);
    }

    private void AddAuthHeaders(HttpRequestHeaders headers, FlowiseCustomMcpServerEntity entity)
    {
        var authConfigJson = string.IsNullOrWhiteSpace(entity.AuthConfigCipherText)
            ? "{}"
            : secretProtector.Unprotect(entity.AuthConfigCipherText);
        using var document = JsonDocument.Parse(authConfigJson);
        if (document.RootElement.TryGetProperty("headers", out var headerElement))
        {
            AddConfiguredHeaders(headers, headerElement);
        }

        if (entity.AuthType.Equals("bearer", StringComparison.OrdinalIgnoreCase)
            && document.RootElement.TryGetProperty("token", out var tokenElement))
        {
            var token = tokenElement.GetString();
            if (!string.IsNullOrWhiteSpace(token))
            {
                headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
            }
        }
    }

    private static void AddConfiguredHeaders(HttpHeaders headers, JsonElement headerElement)
    {
        if (headerElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in headerElement.EnumerateObject())
            {
                TryAddHeader(headers, property.Name, property.Value.GetString());
            }
        }
        else if (headerElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in headerElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var key = item.TryGetProperty("key", out var keyElement) ? keyElement.GetString() : null;
                var value = item.TryGetProperty("value", out var valueElement) ? valueElement.GetString() : null;
                TryAddHeader(headers, key, value);
            }
        }
    }

    private static void TryAddHeader(HttpHeaders headers, string? key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var headerName = key.Trim();
        if (headerName.Equals("Host", StringComparison.OrdinalIgnoreCase)
            || headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        headers.TryAddWithoutValidation(headerName, value.Trim());
    }

    private static IReadOnlyList<FlowiseCustomMcpServerToolDto> ParseToolsListResponse(string body)
    {
        using var document = JsonDocument.Parse(body);
        if (document.RootElement.TryGetProperty("error", out var errorElement))
        {
            throw new ValidationException($"MCP Server error: {errorElement.GetRawText()}", ErrorCodes.ParameterInvalid);
        }

        if (!document.RootElement.TryGetProperty("result", out var resultElement)
            || !resultElement.TryGetProperty("tools", out var toolsElement)
            || toolsElement.ValueKind != JsonValueKind.Array)
        {
            throw new ValidationException("MCP Server response missing result.tools array", ErrorCodes.ParameterInvalid);
        }

        return toolsElement.EnumerateArray().Select(ParseTool).Where(item => item.Name.Length > 0).ToList();
    }

    private static FlowiseCustomMcpServerToolDto ParseTool(JsonElement toolElement)
    {
        var name = toolElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;
        var description = toolElement.TryGetProperty("description", out var descriptionElement) ? descriptionElement.GetString() : null;
        var inputSchema = toolElement.TryGetProperty("inputSchema", out var inputSchemaElement) ? inputSchemaElement.GetRawText() : "{}";
        var annotations = toolElement.TryGetProperty("annotations", out var annotationsElement) ? annotationsElement.GetRawText() : "{}";
        var icons = toolElement.TryGetProperty("icons", out var iconsElement) ? iconsElement.GetRawText() : "[]";
        return new FlowiseCustomMcpServerToolDto
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            InputSchemaJson = string.IsNullOrWhiteSpace(inputSchema) ? "{}" : inputSchema,
            AnnotationsJson = string.IsNullOrWhiteSpace(annotations) ? "{}" : annotations,
            IconsJson = string.IsNullOrWhiteSpace(icons) ? "[]" : icons
        };
    }

    private static IReadOnlyList<FlowiseCustomMcpServerToolDto> ParseStoredTools(string toolsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<FlowiseCustomMcpServerToolDto>>(toolsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static FlowiseCustomMcpServerUpsertRequest Normalize(FlowiseCustomMcpServerUpsertRequest request, bool requireAuthConfig)
    {
        request.Name = FlowiseResourceJson.Required(request.Name, "Custom MCP Server name");
        request.ServerUrl = NormalizeServerUrl(request.ServerUrl);
        request.AuthType = NormalizeAuthType(request.AuthType);
        request.Status = NormalizeStatus(request.Status);
        request.AuthConfigJson = NormalizeAuthConfig(request.AuthConfigJson, requireAuthConfig);
        return request;
    }

    private static string NormalizeServerUrl(string? serverUrl)
    {
        var normalized = FlowiseResourceJson.Required(serverUrl, "Custom MCP Server URL");
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new ValidationException("Custom MCP Server URL 必须是 http 或 https 绝对地址", ErrorCodes.ParameterInvalid);
        }

        return uri.ToString();
    }

    private static string NormalizeAuthType(string? authType)
    {
        var normalized = string.IsNullOrWhiteSpace(authType) ? "none" : authType.Trim().ToLowerInvariant();
        return normalized is "none" or "bearer" or "headers" or "custom"
            ? normalized
            : throw new ValidationException("Custom MCP Server authType 必须是 none、bearer、headers 或 custom", ErrorCodes.ParameterInvalid);
    }

    private static string NormalizeStatus(string? status)
    {
        var normalized = string.IsNullOrWhiteSpace(status) ? "Enabled" : status.Trim();
        return normalized is "Enabled" or "Disabled" or "Authorized" or "Error"
            ? normalized
            : throw new ValidationException("Custom MCP Server 状态必须是 Enabled、Disabled、Authorized 或 Error", ErrorCodes.ParameterInvalid);
    }

    private static string NormalizeAuthConfig(string? json, bool requireAuthConfig)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return requireAuthConfig ? "{}" : string.Empty;
        }

        return FlowiseResourceJson.NormalizeObject(json, "Custom MCP Server auth config");
    }

    private static string MaskAuthConfig(string authConfigJson)
    {
        if (string.IsNullOrWhiteSpace(authConfigJson))
        {
            return "{}";
        }

        using var document = JsonDocument.Parse(authConfigJson);
        var mutable = JsonSerializer.Deserialize<Dictionary<string, object?>>(document.RootElement.GetRawText(), JsonOptions) ?? [];
        if (document.RootElement.TryGetProperty("headers", out var headerElement))
        {
            mutable["headers"] = MaskHeaders(headerElement);
        }

        if (mutable.ContainsKey("token"))
        {
            mutable["token"] = RedactedValue;
        }

        return JsonSerializer.Serialize(mutable, JsonOptions);
    }

    private static object MaskHeaders(JsonElement headerElement)
    {
        if (headerElement.ValueKind == JsonValueKind.Object)
        {
            return headerElement.EnumerateObject().ToDictionary(property => property.Name, _ => RedactedValue);
        }

        if (headerElement.ValueKind == JsonValueKind.Array)
        {
            return headerElement.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .Select(item => new
                {
                    key = item.TryGetProperty("key", out var keyElement) ? keyElement.GetString() : string.Empty,
                    value = RedactedValue
                })
                .ToList();
        }

        return new Dictionary<string, string>();
    }

    private void Apply(FlowiseCustomMcpServerEntity entity, FlowiseCustomMcpServerUpsertRequest request, bool preserveExistingAuthConfig)
    {
        entity.WorkspaceId = string.IsNullOrWhiteSpace(request.WorkspaceId) ? null : request.WorkspaceId.Trim();
        entity.Name = request.Name.Trim();
        entity.ServerUrl = request.ServerUrl.Trim();
        entity.IconSrc = string.IsNullOrWhiteSpace(request.IconSrc) ? null : request.IconSrc.Trim();
        entity.Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim();
        entity.AuthType = request.AuthType ?? "none";
        entity.Status = request.Status ?? "Enabled";
        if (!preserveExistingAuthConfig)
        {
            var authConfigJson = request.AuthConfigJson ?? "{}";
            if (authConfigJson.Contains(RedactedValue, StringComparison.Ordinal))
            {
                authConfigJson = MergeRedactedAuthConfig(authConfigJson, entity.AuthConfigCipherText);
            }

            entity.AuthConfigCipherText = secretProtector.Protect(authConfigJson);
            entity.AuthConfigMaskJson = MaskAuthConfig(authConfigJson);
        }
    }

    private string MergeRedactedAuthConfig(string requestedJson, string? existingCipherText)
    {
        var existingJson = string.IsNullOrWhiteSpace(existingCipherText) ? "{}" : secretProtector.Unprotect(existingCipherText);
        using var requestedDocument = JsonDocument.Parse(requestedJson);
        using var existingDocument = JsonDocument.Parse(existingJson);
        RejectPartialRedactedValues(requestedDocument.RootElement);

        var requested = JsonSerializer.Deserialize<Dictionary<string, object?>>(requestedDocument.RootElement.GetRawText(), JsonOptions) ?? [];
        if (requestedDocument.RootElement.TryGetProperty("headers", out var requestedHeaders)
            && existingDocument.RootElement.TryGetProperty("headers", out var existingHeaders))
        {
            requested["headers"] = MergeRedactedHeaders(requestedHeaders, existingHeaders);
        }

        if (requestedDocument.RootElement.TryGetProperty("token", out var requestedToken)
            && requestedToken.GetString() == RedactedValue
            && existingDocument.RootElement.TryGetProperty("token", out var existingToken))
        {
            requested["token"] = existingToken.GetString();
        }

        return JsonSerializer.Serialize(requested, JsonOptions);
    }

    private static object MergeRedactedHeaders(JsonElement requestedHeaders, JsonElement existingHeaders)
    {
        var existing = HeadersToDictionary(existingHeaders);
        return HeadersToDictionary(requestedHeaders)
            .ToDictionary(pair => pair.Key, pair => pair.Value == RedactedValue && existing.TryGetValue(pair.Key, out var oldValue) ? oldValue : pair.Value);
    }

    private static Dictionary<string, string> HeadersToDictionary(JsonElement headerElement)
    {
        if (headerElement.ValueKind == JsonValueKind.Object)
        {
            return headerElement.EnumerateObject()
                .Where(property => !string.IsNullOrWhiteSpace(property.Name))
                .ToDictionary(property => property.Name, property => property.Value.GetString() ?? string.Empty);
        }

        if (headerElement.ValueKind == JsonValueKind.Array)
        {
            return headerElement.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .Select(item => new
                {
                    Key = item.TryGetProperty("key", out var keyElement) ? keyElement.GetString() ?? string.Empty : string.Empty,
                    Value = item.TryGetProperty("value", out var valueElement) ? valueElement.GetString() ?? string.Empty : string.Empty
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .ToDictionary(item => item.Key, item => item.Value);
        }

        return [];
    }

    private static void RejectPartialRedactedValues(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    RejectPartialRedactedValues(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    RejectPartialRedactedValues(item);
                }
                break;
            case JsonValueKind.String:
                var value = element.GetString();
                if (!string.IsNullOrEmpty(value)
                    && value.Contains(RedactedValue, StringComparison.Ordinal)
                    && value != RedactedValue)
                {
                    throw new ValidationException("Custom MCP Server auth config 包含不完整的脱敏占位符，请重新输入完整值", ErrorCodes.ParameterInvalid);
                }
                break;
        }
    }

    private async Task EnsureUniqueNameAsync(string name, string? exceptId, CancellationToken cancellationToken)
    {
        var duplicate = await db.Queryable<FlowiseCustomMcpServerEntity>()
            .AnyAsync(item => !item.IsDeleted && item.Name == name && (exceptId == null || item.Id != exceptId), cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Custom MCP Server name 已存在", ErrorCodes.ParameterInvalid);
        }
    }

    private async Task<FlowiseCustomMcpServerEntity> LoadAsync(string id, CancellationToken cancellationToken)
    {
        return await db.Queryable<FlowiseCustomMcpServerEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id.Trim(), cancellationToken)
            ?? throw new ValidationException("Custom MCP Server 不存在", ErrorCodes.ParameterInvalid);
    }

    private async Task WriteAuditAsync(string eventType, string resourceId, string detail, CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        var detailJson = JsonSerializer.Serialize(new { detail }, JsonOptions);
        await db.Insertable(new FlowiseAuditLogEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            EventType = eventType,
            ResourceType = ResourceType,
            ResourceId = resourceId,
            DetailJson = detailJson
        }).ExecuteCommandAsync(cancellationToken);
    }

    private static FlowiseCustomMcpServerDto Map(FlowiseCustomMcpServerEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        ServerUrl = entity.ServerUrl,
        IconSrc = entity.IconSrc,
        Color = entity.Color,
        AuthType = entity.AuthType,
        AuthConfigJson = entity.AuthConfigMaskJson,
        ToolsJson = entity.ToolsJson,
        ToolCount = entity.ToolCount,
        Status = entity.Status,
        ErrorMessage = entity.ErrorMessage,
        WorkspaceId = entity.WorkspaceId,
        CreatedTime = entity.CreatedTime,
        UpdatedTime = entity.UpdatedTime
    };

    private static int NormalizePage(int value) => value <= 0 ? 1 : value;

    private static int NormalizeSize(int value) => Math.Clamp(value <= 0 ? 20 : value, 1, 500);
}
