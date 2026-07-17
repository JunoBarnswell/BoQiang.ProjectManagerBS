using System;
using System.Collections.Generic;

namespace AsterERP.Workflow.Core.Services;

public sealed record ApprovalHistoryEntry
{
    public string EntryType { get; init; } = null!;
    public string? TaskId { get; init; }
    public string? ActivityId { get; init; }
    public string? ActivityName { get; init; }
    public string? Assignee { get; init; }
    public string? Message { get; init; }
    public string? Action { get; init; }
    public DateTime? Time { get; init; }
    public IReadOnlyDictionary<string, object?> Variables { get; init; } = new Dictionary<string, object?>();
}
