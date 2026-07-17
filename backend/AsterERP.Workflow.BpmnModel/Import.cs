using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class Import
{
    public string? ImportType { get; set; }
    public string? Location { get; set; }
    public string? Namespace { get; set; }
}

