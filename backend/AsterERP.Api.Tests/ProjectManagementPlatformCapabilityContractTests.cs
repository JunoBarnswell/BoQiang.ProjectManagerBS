using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.SignalR;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementPlatformCapabilityContractTests
{
    [Fact]
    public void Contracts_expose_workspace_trace_and_idempotency_boundaries()
    {
        var notification = new ProjectManagementNotification(
            "tenant-a",
            "MES",
            "task.assigned",
            "user-a",
            "任务已分配",
            "请查看任务",
            "/projects/project-a/tasks/task-a",
            "trace-a");
        var job = new ProjectManagementReminderJobArgs(
            "reminder-a",
            "tenant-a",
            "MES",
            "user-a",
            1);

        Assert.Equal("tenant-a", notification.TenantId);
        Assert.Equal("MES", notification.AppCode);
        Assert.Equal("trace-a", notification.TraceId);
        Assert.Equal("reminder-a", job.ReminderId);
        Assert.Equal("user-a", job.RecipientUserId);
        Assert.Equal(1, job.VersionNo);
        Assert.DoesNotContain(typeof(ProjectManagementReminderJobArgs).GetProperties(), property => property.Name.Contains("payload", StringComparison.OrdinalIgnoreCase) || property.Name.Contains("note", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("pm:project:tenant-a:MES:project-a", SystemNotificationHub.BuildProjectManagementProjectGroupName("tenant-a", "MES", "project-a"));
    }

    [Fact]
    public void Capability_contracts_are_replaceable_interfaces()
    {
        Assert.True(typeof(IProjectManagementFileReferenceService).IsInterface);
        Assert.True(typeof(IProjectManagementFileStore).IsInterface);
        Assert.True(typeof(IProjectManagementNotificationPublisher).IsInterface);
        Assert.True(typeof(IProjectManagementRealtimeTransport).IsInterface);
        Assert.True(typeof(IProjectManagementRealtimePublisher).IsInterface);
        Assert.True(typeof(IProjectManagementReminderScheduler).IsInterface);
        Assert.True(typeof(IProjectManagementActivityWriter).IsInterface);
    }
}
