namespace AsterERP.Workflow.Api;

public class TaskImplementation : ITask
{
    public string Id { get; set; } = null!;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? Priority { get; set; }
    public string? Assignee { get; set; }
    public string? Owner { get; set; }
    public string? ProcessInstanceId { get; set; }
    public string? ProcessDefinitionId { get; set; }
    public string? ExecutionId { get; set; }
    public string? TaskDefinitionKey { get; set; }
    public System.DateTime? CreatedDate { get; set; }
    public System.DateTime? DueDate { get; set; }
    public string? Category { get; set; }
    public string? FormKey { get; set; }
    public string? TenantId { get; set; }
}
