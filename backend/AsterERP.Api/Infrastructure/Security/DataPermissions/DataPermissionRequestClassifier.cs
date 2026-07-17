namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

public sealed class DataPermissionRequestClassifier
{
    public bool IsRuntimeWorkspaceApi(string? path) =>
        StartsWith(path, "/api/runtime/");

    public bool IsApplicationDataCenterApi(string? path) =>
        StartsWith(path, "/api/application-data-center/") ||
        StartsWith(path, "/api/application-data/");

    public bool IsApplicationDevelopmentCenterApi(string? path) =>
        StartsWith(path, "/api/application-development-center/");

    public bool IsAiWorkspaceApi(string? path) =>
        StartsWith(path, "/api/ai/") ||
        StartsWith(path, "/api/v1/webhook-listener/") ||
        StartsWith(path, "/api/v1/webhook/") ||
        StartsWith(path, "/api/v1/node-icon/");

    public bool IsWorkflowWorkspaceApi(string? path) =>
        StartsWith(path, "/api/workflows/");

    public bool IsSystemAdministrationApi(string? path) =>
        StartsWith(path, "/api/system/");

    public bool IsAsterSceneApi(string? path) =>
        StartsWith(path, "/api/asterscene/") ||
        StartsWith(path, "/api/creator/") ||
        StartsWith(path, "/api/community/") ||
        StartsWith(path, "/api/subscriptions/") ||
        StartsWith(path, "/api/usage/") ||
        StartsWith(path, "/api/admin/asterscene/");

    public bool IsAsterScenePublicReadApi(string? path) =>
        StartsWith(path, "/api/public/asterscene/") ||
        StartsWith(path, "/api/community/asterscene/");

    public bool IsImApi(string? path) =>
        StartsWith(path, "/api/im/");

    private static bool StartsWith(string? path, string prefix) =>
        !string.IsNullOrWhiteSpace(path) &&
        path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
