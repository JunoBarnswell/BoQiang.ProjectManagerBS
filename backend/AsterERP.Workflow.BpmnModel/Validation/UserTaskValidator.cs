using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel.Validation;

public class UserTaskValidator : ProcessLevelValidator
{
    public override string ValidatorName => "UserTaskValidator";

    protected override void ExecuteValidation(BpmnModel model, Process process, List<ValidationError> errors)
    {
        var userTasks = FindFlowElementsOfType<UserTask>(process);

        foreach (var userTask in userTasks)
        {
            ValidateAssignee(process, userTask, errors);
            ValidateFormKey(process, userTask, errors);
        }
    }

    private void ValidateAssignee(Process process, UserTask userTask, List<ValidationError> errors)
    {
        if (string.IsNullOrEmpty(userTask.Assignee) &&
            (userTask.CandidateUsers == null || userTask.CandidateUsers.Count == 0) &&
            (userTask.CandidateGroups == null || userTask.CandidateGroups.Count == 0))
        {
            AddWarning(errors, "USER_TASK_NO_ASSIGNEE",
                "User task should have an assignee or candidateUsers/candidateGroups defined", process, userTask);
        }
    }

    private void ValidateFormKey(Process process, UserTask userTask, List<ValidationError> errors)
    {
        if (!string.IsNullOrEmpty(userTask.FormKey) && userTask.FormKey.Contains(' '))
        {
            AddWarning(errors, "USER_TASK_INVALID_FORM_KEY",
                "User task formKey contains spaces which may cause issues", process, userTask);
        }
    }
}
