using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class DataGrid : BaseElement
{
    public string? Name { get; set; }
    public List<DataGridField> Fields { get; set; } = new();
    public List<DataGridRow> Rows { get; set; } = new();

    public override BaseElement Clone()
    {
        var clone = new DataGrid { Id = Id, Name = Name };
        clone.Fields.AddRange(Fields.Select(f => (DataGridField)f.Clone()));
        clone.Rows.AddRange(Rows.Select(r => (DataGridRow)r.Clone()));
        return clone;
    }
}

