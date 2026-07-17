using System.Text.Json;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.Extensions.Logging;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationMicroflowRuntimePermissionService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    IRuntimePageSchemaService pageSchemaService,
    RuntimeDataReadPermissionService readPermissionService,
    RuntimeDataMutationPermissionService mutationPermissionService,
    ICurrentUser currentUser,
    ILogger<ApplicationMicroflowRuntimePermissionService> logger)
{
    public async Task EnsureAsync(
        string flowCode,
        ApplicationMicroflowExecuteRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PageCode))
        {
            logger.LogWarning(
                "Runtime microflow permission denied because PageCode is missing. FlowCode={FlowCode} ModelCode={ModelCode} Action={Action}",
                flowCode,
                request.ModelCode,
                request.Action);
            throw new ValidationException("运行页微流必须提供页面编码", ErrorCodes.PermissionDenied);
        }

        var definition = await ReadPublishedDefinitionAsync(flowCode, cancellationToken);
        var checks = BuildPermissionChecks(definition, request.ModelCode);
        var requestedAction = NormalizeRequestAction(request.Action);
        await pageSchemaService.GetPublishedPageAsync(request.PageCode, request.PreviewPageId, cancellationToken);
        EnsurePageActionPermission(request.PageCode, requestedAction);
        if (checks.Count == 0)
        {
            logger.LogDebug(
                "Runtime microflow permission resolved by page permission because the flow has no model-bound runtime nodes. FlowCode={FlowCode} PageCode={PageCode} Action={Action}",
                flowCode,
                request.PageCode,
                requestedAction);
            return;
        }

        var distinctChecks = checks.Distinct().ToArray();
        logger.LogDebug(
            "Runtime microflow permission checks resolved. FlowCode={FlowCode} PageCode={PageCode} CheckCount={CheckCount} Checks={Checks}",
            flowCode,
            request.PageCode,
            distinctChecks.Length,
            string.Join(",", distinctChecks.Select(check => $"{check.ModelCode}:{check.Action}")));

        foreach (var check in distinctChecks)
        {
            if (check.Action == "view")
            {
                await readPermissionService.EnsureAsync(check.ModelCode, request.PageCode, request.PreviewPageId, cancellationToken);
                continue;
            }

            await mutationPermissionService.EnsureAsync(check.ModelCode, request.PageCode, request.PreviewPageId, check.Action, cancellationToken);
        }

        logger.LogInformation(
            "Runtime microflow permission granted. FlowCode={FlowCode} PageCode={PageCode} CheckCount={CheckCount}",
            flowCode,
            request.PageCode,
            distinctChecks.Length);
    }

    private async Task<ApplicationMicroflowDefinition> ReadPublishedDefinitionAsync(
        string flowCode,
        CancellationToken cancellationToken)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = (await db.Queryable<ApplicationMicroflowEntity>()
            .Where(item =>
                item.ObjectCode == flowCode &&
                item.ModuleKey == ApplicationDataCenterModuleKey.Microflow &&
                item.Status == ApplicationDataCenterObjectStatus.Published &&
                !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("微流不存在或未发布", ErrorCodes.ApplicationDataCenterObjectNotFound);

        return ApplicationMicroflowDefinitionReader.Read(entity.ConfigJson);
    }

    private static List<RuntimeMicroflowPermissionCheck> BuildPermissionChecks(
        ApplicationMicroflowDefinition definition,
        string? fallbackModelCode)
    {
        var checks = new List<RuntimeMicroflowPermissionCheck>();
        foreach (var node in definition.Nodes)
        {
            var nodeType = node.Type.Trim();
            if (nodeType.Equals("retrieve", StringComparison.OrdinalIgnoreCase) ||
                nodeType.Equals("query", StringComparison.OrdinalIgnoreCase) ||
                nodeType.Equals("detail", StringComparison.OrdinalIgnoreCase))
            {
                AddCheck(checks, ReadString(node.Config, "modelCode") ?? fallbackModelCode, "view");
                continue;
            }

            if (nodeType.Equals("compositeDetail", StringComparison.OrdinalIgnoreCase))
            {
                AddCheck(checks, ReadString(node.Config, "rootModelCode") ?? fallbackModelCode, "view");
                AddChildChecks(checks, node.Config, "view");
                continue;
            }

            if (nodeType.Equals("create", StringComparison.OrdinalIgnoreCase))
            {
                AddCheck(checks, ReadString(node.Config, "modelCode") ?? fallbackModelCode, "add");
                continue;
            }

            if (nodeType.Equals("change", StringComparison.OrdinalIgnoreCase) ||
                nodeType.Equals("compositeUpdate", StringComparison.OrdinalIgnoreCase))
            {
                AddCheck(checks, ReadString(node.Config, "modelCode") ?? ReadString(node.Config, "rootModelCode") ?? fallbackModelCode, "edit");
                AddChildChecks(checks, node.Config, "edit");
                if (nodeType.Equals("compositeUpdate", StringComparison.OrdinalIgnoreCase))
                {
                    AddCompositeUpdateChildDeleteChecks(checks, node.Config);
                }

                continue;
            }

            if (nodeType.Equals("delete", StringComparison.OrdinalIgnoreCase) ||
                nodeType.Equals("compositeDelete", StringComparison.OrdinalIgnoreCase))
            {
                AddCheck(checks, ReadString(node.Config, "modelCode") ?? ReadString(node.Config, "rootModelCode") ?? fallbackModelCode, "delete");
                AddChildChecks(checks, node.Config, "delete");
                continue;
            }

            if (nodeType.Equals("compositeCreate", StringComparison.OrdinalIgnoreCase))
            {
                AddCheck(checks, ReadString(node.Config, "rootModelCode") ?? fallbackModelCode, "add");
                AddChildChecks(checks, node.Config, "add");
            }
        }

        return checks;
    }

    private static void AddChildChecks(
        List<RuntimeMicroflowPermissionCheck> checks,
        IReadOnlyDictionary<string, object?> config,
        string action)
    {
        foreach (var childModelCode in ReadChildModelCodes(config))
        {
            AddCheck(checks, childModelCode, action);
        }
    }

    private static void AddCompositeUpdateChildDeleteChecks(
        List<RuntimeMicroflowPermissionCheck> checks,
        IReadOnlyDictionary<string, object?> config)
    {
        foreach (var child in ReadChildConfigs(config))
        {
            var modelCode = ReadString(child, "modelCode");
            if (string.IsNullOrWhiteSpace(modelCode) || !CompositeUpdateChildCanDelete(child))
            {
                continue;
            }

            AddCheck(checks, modelCode, "delete");
        }
    }

    private static void AddCheck(List<RuntimeMicroflowPermissionCheck> checks, string? modelCode, string action)
    {
        if (string.IsNullOrWhiteSpace(modelCode))
        {
            return;
        }

        checks.Add(new RuntimeMicroflowPermissionCheck(modelCode.Trim(), action));
    }

    private static IEnumerable<string> ReadChildModelCodes(IReadOnlyDictionary<string, object?> config)
    {
        foreach (var child in ReadChildConfigs(config))
        {
            var modelCode = ReadString(child, "modelCode");
            if (!string.IsNullOrWhiteSpace(modelCode))
            {
                yield return modelCode;
            }
        }
    }

    private static IEnumerable<Dictionary<string, object?>> ReadChildConfigs(IReadOnlyDictionary<string, object?> config)
    {
        if (!config.TryGetValue("children", out var value) || value is null)
        {
            yield break;
        }

        var children = value is JsonElement element
            ? JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(element.GetRawText(), ApplicationDataCenterJson.Options)
            : JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(JsonSerializer.Serialize(value, ApplicationDataCenterJson.Options), ApplicationDataCenterJson.Options);

        foreach (var child in children ?? [])
        {
            yield return child;
        }
    }

    private static bool CompositeUpdateChildCanDelete(IReadOnlyDictionary<string, object?> child)
    {
        if (ReadBoolean(child, "deleteMissing"))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(ReadString(child, "deleteIdsExpression")))
        {
            return true;
        }

        if (!child.TryGetValue("deleteIds", out var deleteIds) || deleteIds is null)
        {
            return false;
        }

        if (deleteIds is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Array => element.GetArrayLength() > 0,
                JsonValueKind.String => !string.IsNullOrWhiteSpace(element.GetString()),
                _ => false
            };
        }

        return deleteIds is global::System.Collections.IEnumerable enumerable && enumerable.Cast<object?>().Any();
    }

    private static bool ReadBoolean(IReadOnlyDictionary<string, object?> source, string key)
    {
        if (!source.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        if (value is bool boolean)
        {
            return boolean;
        }

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            return element.ValueKind == JsonValueKind.String &&
                bool.TryParse(element.GetString(), out var jsonBoolean) &&
                jsonBoolean;
        }

        return bool.TryParse(value.ToString(), out var parsedBoolean) && parsedBoolean;
    }

    private static string NormalizeRequestAction(string? action) =>
        action?.Trim().ToLowerInvariant() switch
        {
            "add" or "create" => "add",
            "edit" or "update" => "edit",
            "delete" => "delete",
            _ => "view"
        };

    private void EnsurePageActionPermission(string pageCode, string action)
    {
        var permissionCode = PermissionCodes.BuildAppRuntimePagePermission(pageCode, action);
        if (!currentUser.HasAsterErpPermission(permissionCode))
        {
            throw new ValidationException("无权限执行该运行时页面动作", ErrorCodes.PermissionDenied);
        }
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> source, string key)
    {
        if (!source.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value is JsonElement element ? element.ToString()?.Trim() : value.ToString()?.Trim();
    }

    private sealed record RuntimeMicroflowPermissionCheck(string ModelCode, string Action);
}
