using System.Collections.Generic;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Query;

public class ProcessInstanceQuery : ProcessInstanceQueryImpl
{
    public ProcessInstanceQuery(IEnumerable<ExecutionRecord> source) : base(source) { }

    public ProcessInstanceQuery ByProcessDefinitionId(string processDefinitionId) { ProcessDefinitionId(processDefinitionId); return this; }
    public ProcessInstanceQuery ByBusinessKey(string businessKey) { ProcessInstanceBusinessKey(businessKey); return this; }
    public new ProcessInstanceQuery Active() { base.Active(); return this; }
}
