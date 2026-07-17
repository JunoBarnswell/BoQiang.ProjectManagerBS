using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel;

public class WorkflowExtensionListener : BaseElement
{
    public string? Event { get; set; }
    public string? ImplementationType { get; set; }
    public string? Implementation { get; set; }
    public List<FieldExtension> FieldExtensions { get; set; } = new();

    public override BaseElement Clone()
    {
        var clone = new WorkflowExtensionListener
        {
            Event = Event,
            ImplementationType = ImplementationType,
            Implementation = Implementation
        };
        clone.FieldExtensions.AddRange(FieldExtensions.Select(f => (FieldExtension)f.Clone()));
        return clone;
    }
}

public class FieldExtension : BaseElement
{
    public string? FieldName { get; set; }
    public string? StringValue { get; set; }
    public string? Expression { get; set; }

    public override BaseElement Clone()
    {
        return new FieldExtension
        {
            FieldName = FieldName,
            StringValue = StringValue,
            Expression = Expression
        };
    }
}
