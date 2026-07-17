using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel;

public abstract class TaskWithFieldExtensions : BpmnTask
{
    public List<FieldExtension> FieldExtensions { get; set; } = new();

    protected void SetFieldExtensionValues(TaskWithFieldExtensions otherElement)
    {
        FieldExtensions.Clear();
        FieldExtensions.AddRange(otherElement.FieldExtensions.Select(f => (FieldExtension)f.Clone()));
    }
}

