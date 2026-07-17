using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public interface IReferenceOverrider
{
    void Override(UserTask userTask);
    void Override(StartEvent startEvent);
}

