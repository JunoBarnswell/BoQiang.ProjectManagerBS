using System.Collections.Generic;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Behavior;

public interface IVariablesCalculator
{
    Dictionary<string, object> CalculateOutPutVariables(
        MappingExecutionContext mappingExecutionContext,
        Dictionary<string, object> availableVariables);

    Dictionary<string, object> CalculateInputVariables(ExecutionEntity execution);
}

public class NoneVariablesCalculator : IVariablesCalculator
{
    public Dictionary<string, object> CalculateOutPutVariables(
        MappingExecutionContext mappingExecutionContext,
        Dictionary<string, object> availableVariables)
    {
        return new Dictionary<string, object>();
    }

    public Dictionary<string, object> CalculateInputVariables(ExecutionEntity execution)
    {
        return new Dictionary<string, object>();
    }
}

public class CopyVariablesCalculator : IVariablesCalculator
{
    public bool CopyVariablesToLocalForTasks { get; set; } = true;

    public CopyVariablesCalculator() { }

    public CopyVariablesCalculator(bool copyVariablesToLocalForTasks)
    {
        CopyVariablesToLocalForTasks = copyVariablesToLocalForTasks;
    }

    public Dictionary<string, object> CalculateOutPutVariables(
        MappingExecutionContext mappingExecutionContext,
        Dictionary<string, object> availableVariables)
    {
        if (CopyVariablesToLocalForTasks)
        {
            return new Dictionary<string, object>(availableVariables);
        }
        return new Dictionary<string, object>();
    }

    public Dictionary<string, object> CalculateInputVariables(ExecutionEntity execution)
    {
        if (CopyVariablesToLocalForTasks)
        {
            var result = new Dictionary<string, object>();
            foreach (var kv in execution.Variables)
            {
                if (kv.Value != null)
                {
                    result[kv.Key] = kv.Value;
                }
            }
            return result;
        }
        return new Dictionary<string, object>();
    }
}
