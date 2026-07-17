namespace AsterERP.Workflow.Api;

public class ProcessInstanceImplementation : IProcessInstance
{
    public string Id { get; set; } = null!;
    public string? Name { get; set; }
    public string? BusinessKey { get; set; }
    public string ProcessDefinitionId { get; set; } = null!;
    public string? ProcessDefinitionKey { get; set; }
    public string? ProcessDefinitionName { get; set; }
    public int ProcessDefinitionVersion { get; set; }
    public string? StartUserId { get; set; }
    public System.DateTime? StartTime { get; set; }
    public string? TenantId { get; set; }
    public bool IsEnded { get; set; }
    public bool IsSuspended { get; set; }
}
