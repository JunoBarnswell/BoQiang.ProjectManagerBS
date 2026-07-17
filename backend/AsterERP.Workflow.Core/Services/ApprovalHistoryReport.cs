using System.Collections.Generic;

namespace AsterERP.Workflow.Core.Services;

public sealed record ApprovalHistoryReport
{
    public string ProcessInstanceId { get; init; } = null!;
    public string? ProcessDefinitionId { get; init; }
    public string? BusinessKey { get; init; }
    public string? StartUserId { get; init; }
    public IReadOnlyList<ApprovalHistoryEntry> Entries { get; init; } = new List<ApprovalHistoryEntry>();
}
