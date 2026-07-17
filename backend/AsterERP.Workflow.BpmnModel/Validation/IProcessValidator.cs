namespace AsterERP.Workflow.BpmnModel.Validation;

public interface IProcessValidator
{
    string ValidatorName { get; }
    IEnumerable<ValidationError> Validate(BpmnModel model);
}
