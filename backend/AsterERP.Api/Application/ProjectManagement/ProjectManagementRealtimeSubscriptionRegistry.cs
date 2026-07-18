using System.Collections.Concurrent;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementRealtimeSubscriptionRegistry : IProjectManagementRealtimeSubscriptionRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> subscriptions = new(StringComparer.Ordinal);

    public void Register(string connectionId, string tenantId, string appCode, string projectId, string userId) =>
        subscriptions.GetOrAdd(GroupKey(tenantId, appCode, projectId), _ => new ConcurrentDictionary<string, string>(StringComparer.Ordinal))[connectionId] = userId;

    public void Unregister(string connectionId, string tenantId, string appCode, string projectId)
    {
        var key = GroupKey(tenantId, appCode, projectId);
        if (!subscriptions.TryGetValue(key, out var connections)) return;
        connections.TryRemove(connectionId, out _);
        if (connections.IsEmpty) subscriptions.TryRemove(key, out _);
    }

    public void UnregisterConnection(string connectionId)
    {
        foreach (var (key, connections) in subscriptions)
        {
            connections.TryRemove(connectionId, out _);
            if (connections.IsEmpty) subscriptions.TryRemove(key, out _);
        }
    }

    public IReadOnlyList<string> GetConnectionIds(string tenantId, string appCode, string projectId, string userId)
    {
        if (!subscriptions.TryGetValue(GroupKey(tenantId, appCode, projectId), out var connections)) return [];
        return connections.Where(item => string.Equals(item.Value, userId, StringComparison.OrdinalIgnoreCase)).Select(item => item.Key).ToList();
    }

    private static string GroupKey(string tenantId, string appCode, string projectId) =>
        $"{tenantId.Trim()}\u001f{appCode.Trim().ToUpperInvariant()}\u001f{projectId.Trim()}";
}
