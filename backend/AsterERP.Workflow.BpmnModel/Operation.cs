using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class Operation : BaseElement
{
    public string? Name { get; set; }
    public string? ImplementationRef { get; set; }
    public Message? InMessage { get; set; }
    public Message? OutMessage { get; set; }

    public override BaseElement Clone()
    {
        return new Operation
        {
            Id = Id,
            Name = Name,
            ImplementationRef = ImplementationRef,
            InMessage = InMessage != null ? (Message)InMessage.Clone() : null,
            OutMessage = OutMessage != null ? (Message)OutMessage.Clone() : null
        };
    }
}

