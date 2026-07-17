using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class DataGridField : BaseElement
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public bool Required { get; set; }
    public bool Readable { get; set; } = true;
    public bool Writable { get; set; } = true;

    public override BaseElement Clone()
    {
        return new DataGridField
        {
            Id = Id,
            Name = Name,
            Type = Type,
            Required = Required,
            Readable = Readable,
            Writable = Writable
        };
    }
}

