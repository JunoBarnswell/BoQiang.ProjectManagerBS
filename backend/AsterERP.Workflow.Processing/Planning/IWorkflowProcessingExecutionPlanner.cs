using AsterERP.Workflow.Processing.Definitions;

namespace AsterERP.Workflow.Processing.Planning;

public interface IWorkflowProcessingExecutionPlanner
{
    WorkflowProcessingExecutionPlan Plan(WorkflowProcessingDefinition definition);
}
