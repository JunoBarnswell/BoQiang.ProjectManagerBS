using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class AcceptUpdates : IAcceptUpdates
{
    public bool IsAcceptUpdates { get; set; }

    public virtual void Accept(IReferenceOverrider referenceOverrider)
    {
    }
}

