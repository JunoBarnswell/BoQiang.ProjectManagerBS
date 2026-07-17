using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class Assignment : BaseElement
{
    public string? From { get; set; }
    public string? To { get; set; }

    public override BaseElement Clone()
    {
        return new Assignment { Id = Id, From = From, To = To };
    }
}

