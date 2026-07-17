namespace AsterERP.Workflow.Core.Services;

public record HistoricIdentityLink
{
    public string Id { get; init; } = null!;
    public string? Type { get; init; }
    public string? UserId { get; init; }
    public string? GroupId { get; init; }
    public string? ProcessInstanceId { get; init; }
    public string? TaskId { get; init; }
}
