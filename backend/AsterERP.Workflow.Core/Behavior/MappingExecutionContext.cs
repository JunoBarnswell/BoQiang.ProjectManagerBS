using System;
using System.Collections.Generic;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Behavior;

public class MappingExecutionContext
{
    public string? ProcessDefinitionId { get; set; }
    public string? ActivityId { get; set; }
    public ExecutionEntity? Execution { get; set; }

    public MappingExecutionContext() { }

    public MappingExecutionContext(ExecutionEntity execution)
    {
        ProcessDefinitionId = execution.ProcessDefinitionId;
        ActivityId = execution.CurrentActivityId;
        Execution = execution;
    }

    public MappingExecutionContext(string processDefinitionId, string activityId)
    {
        ProcessDefinitionId = processDefinitionId;
        ActivityId = activityId;
    }

    public bool HasExecution => Execution != null;

    public static MappingExecutionContext BuildMappingExecutionContext(ExecutionEntity execution)
    {
        return new MappingExecutionContext(execution);
    }

    public static MappingExecutionContext BuildMappingExecutionContext(string processDefinitionId, string activityId)
    {
        return new MappingExecutionContext(processDefinitionId, activityId);
    }

    public override bool Equals(object? obj)
    {
        if (this == obj) return true;
        if (obj is not MappingExecutionContext other) return false;
        return ProcessDefinitionId == other.ProcessDefinitionId && ActivityId == other.ActivityId;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ProcessDefinitionId, ActivityId);
    }
}
