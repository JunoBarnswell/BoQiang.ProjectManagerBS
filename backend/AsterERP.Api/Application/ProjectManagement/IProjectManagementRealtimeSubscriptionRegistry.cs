namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementRealtimeSubscriptionRegistry
{
    void Register(string connectionId, string tenantId, string appCode, string projectId, string userId);
    void Unregister(string connectionId, string tenantId, string appCode, string projectId);
    void UnregisterConnection(string connectionId);
    IReadOnlyList<string> GetConnectionIds(string tenantId, string appCode, string projectId, string userId);
}
