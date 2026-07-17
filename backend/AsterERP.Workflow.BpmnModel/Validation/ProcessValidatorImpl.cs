namespace AsterERP.Workflow.BpmnModel.Validation;

public class ProcessValidatorImpl : IProcessValidator
{
    private readonly List<IProcessValidator> _validators = new();

    public string ValidatorName => "ProcessValidator";

    public ProcessValidatorImpl()
    {
        _validators.Add(new FlowElementValidator());
        _validators.Add(new StartEventValidator());
        _validators.Add(new EndEventValidator());
        _validators.Add(new SequenceFlowValidator());
        _validators.Add(new GatewayValidator());
        _validators.Add(new UserTaskValidator());
        _validators.Add(new ServiceTaskValidator());
        _validators.Add(new SubProcessValidator());
        _validators.Add(new BoundaryEventValidator());
        _validators.Add(new IntermediateCatchEventValidator());
        _validators.Add(new ProcessValidatorRules());
    }

    public IEnumerable<ValidationError> Validate(BpmnModel model)
    {
        var errors = new List<ValidationError>();
        foreach (var validator in _validators)
            errors.AddRange(validator.Validate(model));
        return errors;
    }

    public void AddValidator(IProcessValidator validator) => _validators.Add(validator);
    public void RemoveValidator(string validatorName) => _validators.RemoveAll(v => v.ValidatorName == validatorName);
}
