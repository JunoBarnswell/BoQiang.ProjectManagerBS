using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

internal static class RuntimeQueryRecordMapper
{
    public static ExecutionRecord ToExecutionRecord(ExecutionEntity execution)
    {
        return new ExecutionRecord
        {
            Id = execution.Id,
            ProcessInstanceId = execution.ProcessInstanceId,
            ProcessDefinitionId = execution.ProcessDefinitionId,
            ParentId = execution.ParentId,
            CurrentActivityId = execution.CurrentFlowElementId ?? execution.ActivityId ?? execution.CurrentActivityId,
            CurrentActivityName = execution.CurrentFlowElement?.Name ?? execution.CurrentActivityName,
            IsActive = execution.IsActive,
            IsEnded = execution.IsEnded,
            BusinessKey = execution.BusinessKey
        };
    }

    public static VariableInstanceRecord ToVariableInstanceRecord(
        ExecutionEntity execution,
        string variableName,
        object? value)
    {
        return new VariableInstanceRecord
        {
            Id = $"{execution.Id}:{variableName}",
            Name = variableName,
            Type = value?.GetType().Name,
            Value = value,
            ExecutionId = execution.Id,
            ProcessInstanceId = execution.ProcessInstanceId
        };
    }
}
