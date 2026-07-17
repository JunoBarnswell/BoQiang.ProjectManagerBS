namespace AsterERP.Workflow.Api;

public interface IProcessInstance
{
    string Id { get; }
    string? Name { get; }
    string? BusinessKey { get; }
    string ProcessDefinitionId { get; }
    string? ProcessDefinitionKey { get; }
    string? ProcessDefinitionName { get; }
    int ProcessDefinitionVersion { get; }
    string? StartUserId { get; }
    System.DateTime? StartTime { get; }
    string? TenantId { get; }
    bool IsEnded { get; }
    bool IsSuspended { get; }
}
