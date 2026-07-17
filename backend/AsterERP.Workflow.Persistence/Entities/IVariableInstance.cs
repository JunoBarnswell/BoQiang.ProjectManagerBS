namespace AsterERP.Workflow.Persistence.Entities;

public interface IVariableInstance : IEntity, IHasRevision
{
    string? Name { get; set; }
    string? TypeName { get; set; }
    object? Value { get; set; }
    string? ProcessInstanceId { get; set; }
    string? ExecutionId { get; set; }
    string? TaskId { get; set; }
    string? TextValue { get; set; }
    string? TextValue2 { get; set; }
    long? LongValue { get; set; }
    double? DoubleValue { get; set; }
    byte[]? Bytes { get; set; }
}
