using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class DataStore : BaseElement
{
    public string? Name { get; set; }
    public string? ItemSubjectRef { get; set; }
    public string? Capacity { get; set; }
    public bool IsUnlimited { get; set; } = true;

    public override BaseElement Clone()
    {
        return new DataStore
        {
            Id = Id,
            Name = Name,
            ItemSubjectRef = ItemSubjectRef,
            Capacity = Capacity,
            IsUnlimited = IsUnlimited
        };
    }
}

