namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

public sealed class DataPermissionFilterRegistry : IDataPermissionFilterRegistry
{
    private readonly HashSet<Type> workspaceEntityTypes = [];
    private readonly HashSet<Type> aiWorkspaceEntityTypes = [];
    private readonly HashSet<Type> aiOwnedEntityTypes = [];
    private readonly HashSet<Type> workflowWorkspaceEntityTypes = [];
    private readonly HashSet<Type> workflowOwnedEntityTypes = [];
    private readonly HashSet<Type> asterSceneWorkspaceEntityTypes = [];
    private readonly HashSet<Type> imTenantEntityTypes = [];

    public IReadOnlyCollection<Type> WorkspaceEntityTypes => workspaceEntityTypes;

    public IReadOnlyCollection<Type> AiWorkspaceEntityTypes => aiWorkspaceEntityTypes;

    public IReadOnlyCollection<Type> AiOwnedEntityTypes => aiOwnedEntityTypes;

    public IReadOnlyCollection<Type> WorkflowWorkspaceEntityTypes => workflowWorkspaceEntityTypes;

    public IReadOnlyCollection<Type> WorkflowOwnedEntityTypes => workflowOwnedEntityTypes;

    public IReadOnlyCollection<Type> AsterSceneWorkspaceEntityTypes => asterSceneWorkspaceEntityTypes;

    public IReadOnlyCollection<Type> ImTenantEntityTypes => imTenantEntityTypes;

    public void RegisterWorkspaceFilter(Type entityType)
    {
        workspaceEntityTypes.Add(entityType);
    }

    public void RegisterAiWorkspaceFilter(Type entityType)
    {
        aiWorkspaceEntityTypes.Add(entityType);
    }

    public void RegisterAiOwnedFilter(Type entityType)
    {
        aiOwnedEntityTypes.Add(entityType);
    }

    public void RegisterWorkflowWorkspaceFilter(Type entityType)
    {
        workflowWorkspaceEntityTypes.Add(entityType);
    }

    public void RegisterWorkflowOwnedFilter(Type entityType)
    {
        workflowOwnedEntityTypes.Add(entityType);
    }

    public void RegisterAsterSceneWorkspaceFilter(Type entityType)
    {
        asterSceneWorkspaceEntityTypes.Add(entityType);
    }

    public void RegisterImTenantFilter(Type entityType)
    {
        imTenantEntityTypes.Add(entityType);
    }

}
