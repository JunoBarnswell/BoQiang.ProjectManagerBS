using System.Collections.Generic;
using System.Linq;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Query;

public class ProcessDefinitionQuery : ProcessDefinitionQueryImpl
{
    public ProcessDefinitionQuery(IEnumerable<ProcessDefinitionRecord> source) : base(source) { }

    public ProcessDefinitionQuery ByKey(string key) { ProcessDefinitionKey(key); return this; }
    public ProcessDefinitionQuery ByName(string name) { ProcessDefinitionName(name); return this; }
    public ProcessDefinitionQuery ByDeploymentId(string deploymentId) { DeploymentId(deploymentId); return this; }

    public new ProcessDefinitionQuery LatestVersion() { base.LatestVersion(); return this; }
}
