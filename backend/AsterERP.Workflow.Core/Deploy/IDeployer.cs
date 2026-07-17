using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Deploy;

public interface IDeployer
{
    void Deploy(DeploymentEntity deployment, Dictionary<string, object>? deploymentSettings);
    Task DeployAsync(
        DeploymentEntity deployment,
        Dictionary<string, object>? deploymentSettings,
        CancellationToken cancellationToken = default);
}
