namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

public interface IDataPermissionFilterRegistry
{
    void RegisterWorkspaceFilter(Type entityType);

    void RegisterProjectManagementFilter(Type entityType);

    void RegisterAiWorkspaceFilter(Type entityType);

    void RegisterAiOwnedFilter(Type entityType);

    void RegisterWorkflowWorkspaceFilter(Type entityType);

    void RegisterWorkflowOwnedFilter(Type entityType);

    void RegisterAsterSceneWorkspaceFilter(Type entityType);

    void RegisterImTenantFilter(Type entityType);

}
