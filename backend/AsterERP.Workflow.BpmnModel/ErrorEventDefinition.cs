using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class ErrorEventDefinition : EventDefinition
{
    public string? ErrorCode { get; set; }
    public string? ErrorHandlerId { get; set; }

    public override BaseElement Clone()
    {
        return new ErrorEventDefinition
        {
            Id = Id,
            ErrorCode = ErrorCode,
            ErrorHandlerId = ErrorHandlerId
        };
    }
}

