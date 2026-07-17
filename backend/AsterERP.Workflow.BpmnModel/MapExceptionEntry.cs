using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class MapExceptionEntry
{
    public string? ClassName { get; set; }
    public string? ErrorCode { get; set; }
    public bool AndChildren { get; set; }
}

