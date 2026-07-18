namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// Compatibility scope for platform-level project-management records.
/// AppCode remains present in the existing schema and entities, but it is no
/// longer derived from the selected application workspace.
/// </summary>
public static class ProjectManagementPlatformScope
{
    public const string AppCode = "SYSTEM";
}
