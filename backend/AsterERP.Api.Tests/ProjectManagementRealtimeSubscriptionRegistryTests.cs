using AsterERP.Api.Application.ProjectManagement;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementRealtimeSubscriptionRegistryTests
{
    [Fact]
    public void Registry_tracks_connections_per_workspace_project_and_user()
    {
        var registry = new ProjectManagementRealtimeSubscriptionRegistry();

        registry.Register("connection-a", "tenant-a", "MES", "project-a", "user-a");
        registry.Register("connection-b", "tenant-a", "MES", "project-a", "user-a");
        registry.Register("connection-c", "tenant-a", "MES", "project-a", "user-b");
        registry.Register("connection-d", "tenant-b", "MES", "project-a", "user-a");

        Assert.Equal(["connection-a", "connection-b"], registry.GetConnectionIds("tenant-a", "mes", "project-a", "user-a").OrderBy(value => value));

        registry.UnregisterConnection("connection-a");

        Assert.Equal(["connection-b"], registry.GetConnectionIds("tenant-a", "MES", "project-a", "user-a"));
        Assert.Equal(["connection-d"], registry.GetConnectionIds("tenant-b", "MES", "project-a", "user-a"));
    }
}
