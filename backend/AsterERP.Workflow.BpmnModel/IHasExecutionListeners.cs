using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public interface IHasExecutionListeners
{
    List<WorkflowExtensionListener> ExecutionListeners { get; }
}

