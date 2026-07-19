using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementApprovalKeyTests
{
    [Fact]
    public void Business_key_round_trips_entity_and_idempotency_key()
    {
        var key = ProjectManagementApprovalKey.Build("Task", "task-1", "request:2026-07-19");

        Assert.True(ProjectManagementApprovalKey.TryParse(key, out var entityType, out var entityId, out var idempotencyKey));
        Assert.Equal("Task", entityType);
        Assert.Equal("task-1", entityId);
        Assert.Equal("request:2026-07-19", idempotencyKey);
    }

    [Fact]
    public void Business_key_rejects_unknown_entity_type()
    {
        Assert.Throws<ValidationException>(() => ProjectManagementApprovalKey.Build("Unknown", "id", "request-1"));
    }
}
