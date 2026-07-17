using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.BpmnParser;
using AsterERP.Workflow.Core.Behavior;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;
using BpmnModelType = AsterERP.Workflow.BpmnModel.BpmnModel;

namespace AsterERP.Workflow.Core.Deployer;

public class BpmnDeployer
{
    private readonly IExpressionManager _expressionManager;
    private readonly BpmnDeploymentHelper _deploymentHelper;
    private readonly EventSubscriptionManager _eventSubscriptionManager;
    private readonly TimerManager _timerManager;
    private readonly IActivityBehaviorFactory? _behaviorFactory;
    private readonly ParsedDeploymentBuilderFactory _parsedDeploymentBuilderFactory;

    public BpmnDeployer(
        IExpressionManager expressionManager,
        BpmnDeploymentHelper deploymentHelper,
        EventSubscriptionManager eventSubscriptionManager,
        TimerManager timerManager,
        IActivityBehaviorFactory? behaviorFactory = null,
        ParsedDeploymentBuilderFactory? parsedDeploymentBuilderFactory = null)
    {
        _expressionManager = expressionManager;
        _deploymentHelper = deploymentHelper;
        _eventSubscriptionManager = eventSubscriptionManager;
        _timerManager = timerManager;
        _behaviorFactory = behaviorFactory;
        _parsedDeploymentBuilderFactory = parsedDeploymentBuilderFactory ?? new ParsedDeploymentBuilderFactory(expressionManager);
    }

    public async Task<ParsedDeployment> DeployAsync(
        string deploymentId,
        List<string> resourceNames,
        byte[][] resources,
        CancellationToken cancellationToken = default)
    {
        var parsedDeploymentBuilder = _parsedDeploymentBuilderFactory.CreateBuilder(deploymentId);

        for (var i = 0; i < resourceNames.Count; i++)
        {
            var resourceName = resourceNames[i];
            var resourceBytes = resources[i];

            if (resourceName.EndsWith(".bpmn", StringComparison.OrdinalIgnoreCase) ||
                resourceName.EndsWith(".bpmn20.xml", StringComparison.OrdinalIgnoreCase))
            {
                BpmnModelType bpmnModel;
                try
                {
                    var parser = new BpmnXmlParser();
                    var xmlContent = System.Text.Encoding.UTF8.GetString(resourceBytes);
                    bpmnModel = parser.Parse(xmlContent);
                }
                catch
                {
                    // Ignore malformed BPMN resources so deployment can continue with valid resources.
                    continue;
                }

                if (_behaviorFactory != null)
                {
                    BpmnBehaviorBinder.BindBehaviors(bpmnModel, _behaviorFactory);
                }

                parsedDeploymentBuilder.AddBpmnModel(resourceName, bpmnModel);
            }
        }

        var parsedDeployment = parsedDeploymentBuilder.Build();

        await _deploymentHelper.VerifyProcessDefinitionsDoNotOverlapAsync(parsedDeployment, cancellationToken);
        await _deploymentHelper.CopyDeploymentResourcesAsync(parsedDeployment, cancellationToken);

        foreach (var parsedProcessDef in parsedDeployment.ProcessDefinitions)
        {
            var processDef = parsedProcessDef.ProcessDefinition;
            var bpmnModel = parsedProcessDef.BpmnModel;

            await _eventSubscriptionManager.CreateEventSubscriptionsAsync(
                processDef,
                bpmnModel,
                cancellationToken);

            await _timerManager.CreateTimersForProcessAsync(
                processDef,
                bpmnModel,
                cancellationToken);
        }

        return parsedDeployment;
    }

    public async Task UndeployAsync(string deploymentId, CancellationToken cancellationToken = default)
    {
        await _eventSubscriptionManager.DeleteEventSubscriptionsByDeploymentAsync(deploymentId, cancellationToken);
        await _timerManager.CancelTimersByDeploymentAsync(deploymentId, cancellationToken);
    }
}
