using AsterERP.Contracts.ProjectManagement;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementAutomationContractTests
{
    [Theory]
    [InlineData("Project")]
    [InlineData("Milestone")]
    [InlineData("Task")]
    public void Supported_entity_types_are_explicit(string entityType)
    {
        Assert.True(ProjectManagementAutomationEntityTypes.IsSupported(entityType));
    }

    [Fact]
    public void Webhook_payload_keeps_idempotency_and_audit_context()
    {
        var payload = new ProjectManagementAutomationWebhookPayload(
            "task.status-changed", "rule-1", "Task", "task-1", "project-1", "Done", "user-1", null, 7,
            DateTime.UtcNow, "trace-1", new Dictionary<string, object?> { ["source"] = "ProjectManagement" });

        Assert.Equal("rule-1", payload.RuleId);
        Assert.Equal(7, payload.VersionNo);
        Assert.Equal("trace-1", payload.TraceId);
        Assert.Equal("ProjectManagement", payload.Data["source"]);
    }

    [Fact]
    public void Outbound_webhook_contract_has_explicit_event_allowlist_and_stable_event_id()
    {
        Assert.Contains(ProjectManagementWebhookEventTypes.StatusChanged, ProjectManagementWebhookEventTypes.All);
        var payload = new ProjectManagementWebhookEventPayload("event-1", ProjectManagementWebhookEventTypes.CommentCreated,
            DateTimeOffset.UtcNow, "project-1", "TaskComment", "comment-1", "trace-1", new Dictionary<string, string?> { ["activityType"] = "comment.created" });
        Assert.Equal("event-1", payload.EventId);
        Assert.Equal("comment-1", payload.ResourceId);
    }
}
