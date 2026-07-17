namespace AsterERP.Workflow.Core.DynamicBpmn;

public record ProcessDefinitionChange
{
    public string ProcessDefinitionId { get; init; } = null!;
    public string ChangeType { get; init; } = null!;
    public string ElementId { get; init; } = null!;
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public DateTime Timestamp { get; init; } = AbpTimeIdProvider.UtcNow;
}

public class ProcessDefinitionChangeAudit
{
    private readonly List<ProcessDefinitionChange> _changes = new();

    public void RecordChange(string processDefinitionId, string changeType, string elementId, string? oldValue = null, string? newValue = null)
    {
        _changes.Add(new ProcessDefinitionChange
        {
            ProcessDefinitionId = processDefinitionId,
            ChangeType = changeType,
            ElementId = elementId,
            OldValue = oldValue,
            NewValue = newValue
        });
    }

    public IReadOnlyList<ProcessDefinitionChange> GetChanges(string processDefinitionId)
    {
        return _changes.Where(c => c.ProcessDefinitionId == processDefinitionId).ToList().AsReadOnly();
    }

    public IReadOnlyList<ProcessDefinitionChange> GetAllChanges()
    {
        return _changes.AsReadOnly();
    }

    public void ClearChanges(string processDefinitionId)
    {
        _changes.RemoveAll(c => c.ProcessDefinitionId == processDefinitionId);
    }
}

