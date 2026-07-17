using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class ComplexGateway : Gateway
{
    public string? DefaultFlow { get; set; }
    public string? ActivationCondition { get; set; }

    public override BaseElement Clone()
    {
        return new ComplexGateway
        {
            Id = Id,
            Name = Name,
            DefaultFlow = DefaultFlow,
            ActivationCondition = ActivationCondition
        };
    }
}

