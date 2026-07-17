namespace AsterERP.Workflow.Api;

public interface ITask
{
    string Id { get; }
    string? Name { get; }
    string? Description { get; }
    int? Priority { get; }
    string? Assignee { get; }
    string? Owner { get; }
    string? ProcessInstanceId { get; }
    string? ProcessDefinitionId { get; }
    string? ExecutionId { get; }
    string? TaskDefinitionKey { get; }
    System.DateTime? CreatedDate { get; }
    System.DateTime? DueDate { get; }
    string? Category { get; set; }
    string? FormKey { get; }
    string? TenantId { get; }
}
