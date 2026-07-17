namespace AsterERP.Workflow.Core.Integration;

public class IntegrationContextEntity
{
    public string Id { get; set; } = null!;
    public string? ExecutionId { get; set; }
    public string? ProcessInstanceId { get; set; }
    public string? ProcessDefinitionId { get; set; }
    public string? FlowNodeId { get; set; }
    public string? ConnectorId { get; set; }
    public string? CorrelationId { get; set; }
    public string? Status { get; set; }
    public string? ResultType { get; set; }
    public DateTime? CreatedDate { get; set; }
}
