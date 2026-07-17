namespace AsterERP.Shared;

public static partial class PermissionCodes
{
    public const string AppRuntimePageModuleName = "ApplicationRuntimePage";

    public const string AppDevelopmentCenterBusinessObjectView = "app:development-center:business-object:view";

    public static string BuildAppRuntimePagePermission(string pageCode, string action)
    {
        return $"app:runtime-page:{NormalizeRuntimePermissionSegment(pageCode)}:{NormalizeRuntimePermissionSegment(action)}";
    }

    public static IReadOnlyList<string> BuildAppRuntimePagePermissions(string pageCode, bool includeMutations, bool includeImportExport)
    {
        var permissions = new List<string>
        {
            BuildAppRuntimePagePermission(pageCode, "view")
        };

        if (includeMutations)
        {
            permissions.Add(BuildAppRuntimePagePermission(pageCode, "add"));
            permissions.Add(BuildAppRuntimePagePermission(pageCode, "edit"));
            permissions.Add(BuildAppRuntimePagePermission(pageCode, "delete"));
        }

        if (includeImportExport)
        {
            permissions.Add(BuildAppRuntimePagePermission(pageCode, "import"));
            permissions.Add(BuildAppRuntimePagePermission(pageCode, "export"));
        }

        return permissions;
    }

    private static string NormalizeRuntimePermissionSegment(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : value.Trim().ToLowerInvariant().Replace("_", "-");
    }
}
