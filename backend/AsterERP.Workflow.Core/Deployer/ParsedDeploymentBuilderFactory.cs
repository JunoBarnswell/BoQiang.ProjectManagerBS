using AsterERP.Workflow.Core.Expression;

namespace AsterERP.Workflow.Core.Deployer;

public class ParsedDeploymentBuilderFactory
{
    private readonly IExpressionManager? _expressionManager;

    public ParsedDeploymentBuilderFactory(IExpressionManager? expressionManager = null)
    {
        _expressionManager = expressionManager;
    }

    public ParsedDeploymentBuilder CreateBuilder(string deploymentId)
    {
        return new ParsedDeploymentBuilder(deploymentId, _expressionManager);
    }
}
