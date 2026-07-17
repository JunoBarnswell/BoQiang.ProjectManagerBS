using System;

namespace AsterERP.Workflow.Core.Services;

public record HistoricProcessInstance(
    string Id,
    string? ProcessDefinitionId,
    string? BusinessKey,
    DateTime? StartTime,
    DateTime? EndTime
);
