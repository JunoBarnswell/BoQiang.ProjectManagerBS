using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel.Validation;

public class ServiceTaskValidator : ProcessLevelValidator
{
    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "mail", "mule", "camel", "shell", "dmn"
    };

    public override string ValidatorName => "ServiceTaskValidator";

    protected override void ExecuteValidation(BpmnModel model, Process process, List<ValidationError> errors)
    {
        var serviceTasks = FindFlowElementsOfType<ServiceTask>(process);

        foreach (var serviceTask in serviceTasks)
        {
            ValidateImplementation(process, serviceTask, errors);
            ValidateType(process, serviceTask, errors);
        }
    }

    private void ValidateImplementation(Process process, ServiceTask serviceTask, List<ValidationError> errors)
    {
        var hasClass = !string.IsNullOrEmpty(serviceTask.Class);
        var hasExpression = !string.IsNullOrEmpty(serviceTask.Expression);
        var hasDelegateExpression = !string.IsNullOrEmpty(serviceTask.DelegateExpression);
        var hasType = !string.IsNullOrEmpty(serviceTask.Type);
        var hasImplementation = !string.IsNullOrEmpty(serviceTask.Implementation);

        if (!hasClass && !hasExpression && !hasDelegateExpression && !hasType && !hasImplementation)
        {
            AddError(errors, "SERVICE_TASK_MISSING_IMPLEMENTATION",
                "Service task must have an implementation (class, expression, delegateExpression, or type)", process, serviceTask);
        }
    }

    private void ValidateType(Process process, ServiceTask serviceTask, List<ValidationError> errors)
    {
        if (string.IsNullOrEmpty(serviceTask.Type))
            return;

        if (!ValidTypes.Contains(serviceTask.Type))
        {
            AddError(errors, "SERVICE_TASK_INVALID_TYPE",
                $"Service task type '{serviceTask.Type}' is not valid. Valid types: mail, mule, camel, shell, dmn", process, serviceTask);
        }
    }
}
