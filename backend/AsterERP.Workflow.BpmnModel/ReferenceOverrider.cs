using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class ReferenceOverrider : IReferenceOverrider
{
    public string? ReferenceId { get; set; }
    public string? ReferenceType { get; set; }
    public string? OverrideValue { get; set; }

    public virtual void Override(UserTask userTask)
    {
    }

    public virtual void Override(StartEvent startEvent)
    {
    }
}

