using System;

namespace AsterERP.Workflow.Core.Services;

public record JobRecord
{
    public string Id { get; init; } = null!;
    public string? Type { get; init; }
    public int Retries { get; init; }
    public string? ExecutionId { get; init; }
    public string? ProcessInstanceId { get; init; }
    public string? ProcessDefinitionId { get; init; }
    public string? ExceptionMessage { get; init; }
    public DateTime? DueDate { get; init; }
    public string? HandlerType { get; init; }
    public string? TenantId { get; init; }
}
