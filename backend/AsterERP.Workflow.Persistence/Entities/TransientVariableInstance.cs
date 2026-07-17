namespace AsterERP.Workflow.Persistence.Entities;

public class TransientVariableInstance : IVariableInstance
{
    public const string TypeTransient = "transient";

    private string _variableName;
    private object? _variableValue;

    public TransientVariableInstance(string variableName, object? variableValue)
    {
        _variableName = variableName;
        _variableValue = variableValue;
    }

    public string Id { get; set; } = null!;
    public bool IsInserted { get; set; }
    public bool IsUpdated { get; set; }
    public bool IsDeleted { get; set; }
    public int Revision { get; set; }
    public int RevisionNext => Revision + 1;
    public string? Name { get => _variableName; set => _variableName = value ?? string.Empty; }
    public string? TypeName { get; set; } = TypeTransient;
    public object? Value { get => _variableValue; set => _variableValue = value; }
    public string? ProcessInstanceId { get; set; }
    public string? ExecutionId { get; set; }
    public string? TaskId { get; set; }
    public string? TextValue { get; set; }
    public string? TextValue2 { get; set; }
    public long? LongValue { get; set; }
    public double? DoubleValue { get; set; }
    public byte[]? Bytes { get; set; }

    public object? GetPersistentState() => null;
}
