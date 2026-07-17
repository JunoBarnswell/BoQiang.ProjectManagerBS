using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class EventListener : BaseElement
{
    public string? Events { get; set; }
    public string? ImplementationType { get; set; }
    public string? Implementation { get; set; }
    public string? EntityType { get; set; }
    public string? OnTransaction { get; set; }

    public override BaseElement Clone()
    {
        return new EventListener
        {
            Id = Id,
            Events = Events,
            ImplementationType = ImplementationType,
            Implementation = Implementation,
            EntityType = EntityType,
            OnTransaction = OnTransaction
        };
    }
}

