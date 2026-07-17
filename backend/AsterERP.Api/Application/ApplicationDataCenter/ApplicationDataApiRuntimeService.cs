using System.Net.Http.Headers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataApiRuntimeService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    ICurrentUser currentUser,
    IApplicationMicroflowRuntimeService microflowRuntimeService,
    ApplicationMicroflowRuntimePermissionService microflowPermissionService,
    ApplicationDataSourceConnectionFactory connectionFactory,
    ApplicationDataPreviewReader previewReader,
    IHttpClientFactory httpClientFactory,
    ILogger<ApplicationDataApiRuntimeService> logger)
{
    public async Task<object?> ExecuteAsync(
        string routePath,
        string httpMethod,
        IReadOnlyDictionary<string, string?> query,
        JsonElement? body,
        CancellationToken cancellationToken = default)
    {
        var elapsed = Stopwatch.StartNew();
        var normalizedMethod = httpMethod.Trim().ToUpperInvariant();
        logger.LogDebug(
            "Application data API request resolving. RoutePath={RoutePath} HttpMethod={HttpMethod} QueryCount={QueryCount} HasBody={HasBody}",
            routePath,
            normalizedMethod,
            query.Count,
            body is not null);

        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        ApplicationApiServiceEntity? service = null;
        try
        {
            service = await ResolveServiceAsync(db, workspace, routePath, normalizedMethod, cancellationToken);
            EnsurePermission(service);
            var config = ApplicationDataCenterJson.DeserializeDictionary(service.ConfigJson);
            var result = service.ObjectType switch
            {
                ApplicationApiServiceSourceType.Microflow => await ExecuteMicroflowAsync(config, query, body, cancellationToken),
                ApplicationApiServiceSourceType.SqlQuery => await ExecuteSqlQueryAsync(db, workspace, service, config, query, cancellationToken),
                ApplicationApiServiceSourceType.ExternalProxy => await ExecuteExternalProxyAsync(config, normalizedMethod, query, body, cancellationToken),
                ApplicationApiServiceSourceType.Webhook => new { accepted = true, receivedAt = DateTime.UtcNow, body = body?.ToString() },
                _ => throw new ValidationException("当前接口服务类型暂不支持运行", ErrorCodes.ApplicationDataCenterRuntimeFailed)
            };

            logger.LogInformation(
                "Application data API request completed. RoutePath={RoutePath} HttpMethod={HttpMethod} ServiceId={ServiceId} SourceType={SourceType} ElapsedMs={ElapsedMs}",
                routePath,
                normalizedMethod,
                service.Id,
                service.ObjectType,
                elapsed.ElapsedMilliseconds);
            return result;
        }
        catch (ValidationException exception)
        {
            logger.LogWarning(
                exception,
                "Application data API request rejected. RoutePath={RoutePath} HttpMethod={HttpMethod} ServiceId={ServiceId} SourceType={SourceType} ElapsedMs={ElapsedMs}",
                routePath,
                normalizedMethod,
                service?.Id,
                service?.ObjectType,
                elapsed.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Application data API request failed. RoutePath={RoutePath} HttpMethod={HttpMethod} ServiceId={ServiceId} SourceType={SourceType} ElapsedMs={ElapsedMs}",
                routePath,
                normalizedMethod,
                service?.Id,
                service?.ObjectType,
                elapsed.ElapsedMilliseconds);
            throw;
        }
    }

    private async Task<object?> ExecuteMicroflowAsync(
        IReadOnlyDictionary<string, object?> config,
        IReadOnlyDictionary<string, string?> query,
        JsonElement? body,
        CancellationToken cancellationToken)
    {
        var flowCode = Required(ReadString(config, "flowCode"), "接口缺少微流编码");
        var variables = ReadValues(body);
        foreach (var item in query)
        {
            if (!string.IsNullOrWhiteSpace(item.Value))
            {
                variables[item.Key] = item.Value;
            }
        }

        var request = new ApplicationMicroflowExecuteRequest(
            variables,
            ReadString(config, "startNodeId"),
            ReadContextValue(config, query, variables, "pageCode"),
            ReadContextValue(config, query, variables, "previewPageId"),
            ReadContextValue(config, query, variables, "modelCode"),
            ReadContextValue(config, query, variables, "action"));
        if (!string.IsNullOrWhiteSpace(request.PageCode))
        {
            await microflowPermissionService.EnsureAsync(flowCode, request, cancellationToken);
        }

        return await microflowRuntimeService.ExecuteAsync(flowCode, request, cancellationToken);
    }

    private static string? ReadContextValue(
        IReadOnlyDictionary<string, object?> config,
        IReadOnlyDictionary<string, string?> query,
        IReadOnlyDictionary<string, object?> variables,
        string key)
    {
        if (query.TryGetValue(key, out var queryValue) && !string.IsNullOrWhiteSpace(queryValue))
        {
            return queryValue;
        }

        if (variables.TryGetValue(key, out var variableValue) && !string.IsNullOrWhiteSpace(Convert.ToString(variableValue)))
        {
            return Convert.ToString(variableValue);
        }

        return ReadString(config, key);
    }

    private static async Task<ApplicationApiServiceEntity> ResolveServiceAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string routePath,
        string httpMethod,
        CancellationToken cancellationToken)
    {
        var normalizedRoute = NormalizeRoutePath(routePath);
        var normalizedMethod = httpMethod.Trim().ToUpperInvariant();
        var entity = (await db.Queryable<ApplicationApiServiceEntity>()
            .Where(item =>
                item.RoutePath == normalizedRoute &&
                item.HttpMethod == normalizedMethod &&
                item.Status == ApplicationDataCenterObjectStatus.Published &&
                !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        return entity ?? throw new NotFoundException("应用数据接口不存在或未发布", ErrorCodes.ApplicationDataCenterObjectNotFound);
    }

    private void EnsurePermission(ApplicationApiServiceEntity service)
    {
        if (!service.RequiresAuthentication)
        {
            return;
        }

        if (!currentUser.IsAsterErpAuthenticated())
        {
            throw new ValidationException("请先登录", ErrorCodes.AuthenticationRequired);
        }

        if (!string.IsNullOrWhiteSpace(service.PermissionCode) &&
            !currentUser.HasAsterErpPermission(service.PermissionCode))
        {
            throw new ValidationException("无权限访问该应用数据接口", ErrorCodes.PermissionDenied);
        }
    }

    private async Task<ApplicationDataCenterPreviewResponse> ExecuteSqlQueryAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationApiServiceEntity service,
        IReadOnlyDictionary<string, object?> config,
        IReadOnlyDictionary<string, string?> query,
        CancellationToken cancellationToken)
    {
        var dataSourceId = service.SourceObjectId ?? ReadString(config, "dataSourceId");
        if (string.IsNullOrWhiteSpace(dataSourceId))
        {
            throw new ValidationException("SQL 查询接口缺少数据源", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var dataSource = (await db.Queryable<ApplicationDataSourceEntity>()
            .Where(item => item.Id == dataSourceId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("接口数据源不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);

        var maxRows = int.TryParse(ReadQuery(query, "pageSize"), out var requestedRows) ? requestedRows : 50;
        using var sourceDb = await connectionFactory.CreateDatabaseClientAsync(dataSource, cancellationToken);
        return await previewReader.PreviewDatabaseAsync(
            sourceDb,
            ReadString(config, "sql"),
            ReadString(config, "tableName"),
            maxRows,
            cancellationToken);
    }

    private async Task<object?> ExecuteExternalProxyAsync(
        IReadOnlyDictionary<string, object?> config,
        string httpMethod,
        IReadOnlyDictionary<string, string?> query,
        JsonElement? body,
        CancellationToken cancellationToken)
    {
        var baseUrl = Required(ReadString(config, "baseUrl") ?? ReadString(config, "url"), "外部代理缺少 URL");
        var client = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(httpMethod), BuildProxyUrl(baseUrl, query));
        var token = ReadString(config, "token");
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null && httpMethod is not "GET" and not "DELETE")
        {
            request.Content = new StringContent(body.Value.GetRawText(), Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        return new
        {
            statusCode = (int)response.StatusCode,
            response.IsSuccessStatusCode,
            body = TryReadJson(text)
        };
    }

    private static Dictionary<string, object?> ReadValues(JsonElement? body)
    {
        var root = ReadObject(body);
        if (root.TryGetValue("values", out var valuesValue) && valuesValue is JsonElement valuesElement)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(valuesElement.GetRawText(), ApplicationDataCenterJson.Options) ?? [];
        }

        return root;
    }

    private static Dictionary<string, object?> ReadObject(JsonElement? body)
    {
        if (body is null || body.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return [];
        }

        if (body.Value.ValueKind != JsonValueKind.Object)
        {
            throw new ValidationException("请求体必须是 JSON 对象", ErrorCodes.ParameterInvalid);
        }

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(body.Value.GetRawText(), ApplicationDataCenterJson.Options) ?? [];
    }

    private static string NormalizeRoutePath(string value)
    {
        var normalized = value.Trim();
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized;
    }

    private static string BuildProxyUrl(
        string baseUrl,
        IReadOnlyDictionary<string, string?> query)
    {
        var pairs = query
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value ?? string.Empty)}")
            .ToArray();
        return pairs.Length == 0
            ? baseUrl
            : $"{baseUrl}{(baseUrl.Contains('?') ? '&' : '?')}{string.Join('&', pairs)}";
    }

    private static object? TryReadJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<object>(value, ApplicationDataCenterJson.Options);
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static string? ReadQuery(IReadOnlyDictionary<string, string?> query, string key) =>
        query.TryGetValue(key, out var value) ? value : null;

    private static string? ReadString(IReadOnlyDictionary<string, object?> config, string key) =>
        config.TryGetValue(key, out var value) && value is not null ? value.ToString() : null;

    private static string Required(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(message, ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return value.Trim();
    }
}
