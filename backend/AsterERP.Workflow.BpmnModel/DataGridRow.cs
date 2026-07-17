using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class DataGridRow : BaseElement
{
    public List<string> Values { get; set; } = new();

    public override BaseElement Clone()
    {
        var clone = new DataGridRow { Id = Id };
        clone.Values.AddRange(Values);
        return clone;
    }
}

