using AsterERP.Api.Modules.Platform;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationSystemAssignmentService(
    ISqlSugarClient db,
    ApplicationDataCenterWorkspaceResolver workspaceResolver)
{
    private const string ConfigKey = "dataCenterAssignment";

    public async Task<IReadOnlyList<ApplicationSystemAssignmentResponse>> GetListAsync(
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var items = await db.Queryable<SystemTenantAppEntity, SystemApplicationEntity>(
                (tenantApp, app) => tenantApp.AppCode == app.AppCode)
            .Where((tenantApp, app) =>
                tenantApp.TenantId == workspace.TenantId &&
                !tenantApp.IsDeleted &&
                !app.IsDeleted)
            .OrderBy((tenantApp, app) => app.AppCode)
            .Select((tenantApp, app) => new
            {
                TenantApp = tenantApp,
                app.AppName,
                app.Version
            })
            .ToListAsync(cancellationToken);

        return items
            .Select(item => Map(item.TenantApp, item.AppName, item.Version))
            .ToArray();
    }

    public async Task<ApplicationSystemAssignmentResponse> UpdateAsync(
        ApplicationSystemAssignmentUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var appCode = ApplicationDataCenterCodePolicy.NormalizeCode(request.AppCode, "应用系统");
        var tenantApp = await db.Queryable<SystemTenantAppEntity>()
            .Where(item =>
                item.TenantId == workspace.TenantId &&
                item.AppCode == appCode &&
                !item.IsDeleted)
            .FirstAsync(cancellationToken)
            ?? throw new NotFoundException("租户应用不存在", ErrorCodes.PlatformResourceNotFound);
        var app = await db.Queryable<SystemApplicationEntity>()
            .Where(item => item.AppCode == appCode && !item.IsDeleted)
            .FirstAsync(cancellationToken)
            ?? throw new NotFoundException("应用系统不存在", ErrorCodes.PlatformResourceNotFound);

        var root = DeserializeRoot(tenantApp.ConfigJson);
        root[ConfigKey] = new Dictionary<string, object?>
        {
            ["runningVersion"] = ApplicationDataCenterCodePolicy.NormalizeOptional(request.RunningVersion),
            ["noPermissionDisplay"] = NormalizeNoPermissionDisplay(request.NoPermissionDisplay),
            ["authorizedObjectIds"] = request.AuthorizedObjectIds
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
        tenantApp.ConfigJson = ApplicationDataCenterJson.Serialize(root);
        tenantApp.UpdatedBy = workspace.UserId;
        tenantApp.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(tenantApp).ExecuteCommandAsync(cancellationToken);
        return Map(tenantApp, app.AppName, app.Version);
    }

    private static ApplicationSystemAssignmentResponse Map(
        SystemTenantAppEntity tenantApp,
        string appName,
        string? appVersion)
    {
        var root = DeserializeRoot(tenantApp.ConfigJson);
        var config = ReadObject(root, ConfigKey);
        var authorizedObjectIds = ReadStringArray(config, "authorizedObjectIds");
        return new ApplicationSystemAssignmentResponse(
            tenantApp.Id,
            tenantApp.AppCode,
            tenantApp.SystemName ?? appName,
            ReadString(config, "runningVersion") ?? appVersion,
            ReadString(config, "noPermissionDisplay") ?? "Hide",
            authorizedObjectIds,
            tenantApp.ConfigJson,
            tenantApp.Status);
    }

    private static Dictionary<string, object?> DeserializeRoot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        return ApplicationDataCenterJson.Deserialize<Dictionary<string, object?>>(json)
            ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, object?> ReadObject(
        IReadOnlyDictionary<string, object?> root,
        string key)
    {
        if (!root.TryGetValue(key, out var value) || value is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        if (value is IReadOnlyDictionary<string, object?> dictionary)
        {
            return dictionary;
        }

        return ApplicationDataCenterJson.Deserialize<Dictionary<string, object?>>(value.ToString() ?? "{}")
            ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ReadStringArray(IReadOnlyDictionary<string, object?> config, string key)
    {
        if (!config.TryGetValue(key, out var value) || value is null)
        {
            return [];
        }

        if (value is IEnumerable<object> objects)
        {
            return objects.Select(item => item.ToString() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }

        return ApplicationDataCenterJson.Deserialize<List<string>>(value.ToString() ?? "[]") ?? [];
    }

    private static string NormalizeNoPermissionDisplay(string value)
    {
        var normalized = value.Trim();
        if (normalized is not ("Hide" or "Disabled" or "Readonly"))
        {
            throw new ValidationException("无权限显示仅支持 Hide、Disabled、Readonly", ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> config, string key) =>
        config.TryGetValue(key, out var value) && value is not null ? value.ToString() : null;
}
