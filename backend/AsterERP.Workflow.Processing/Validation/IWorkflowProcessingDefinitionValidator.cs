using AsterERP.Workflow.Processing.Definitions;

namespace AsterERP.Workflow.Processing.Validation;

public interface IWorkflowProcessingDefinitionValidator
{
    WorkflowProcessingValidationResult Validate(WorkflowProcessingDefinition definition);
}
