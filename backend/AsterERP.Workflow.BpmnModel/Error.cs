using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class Error : BaseElement
{
    public string? ErrorCode { get; set; }
    public string? Name { get; set; }

    public override BaseElement Clone()
    {
        return new Error { Id = Id, ErrorCode = ErrorCode, Name = Name };
    }
}

