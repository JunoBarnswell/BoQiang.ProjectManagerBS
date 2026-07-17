using System;
using System.Linq;
using AsterERP.Workflow.Core.Connector;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Expression;
using AsterERP.Workflow.Core.Helper;
using AsterERP.Workflow.Core.Job;
using AsterERP.Workflow.Core.Services;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class DefaultActivityBehaviorFactory : IActivityBehaviorFactory
{
    private const string DefaultServiceTaskBeanName = "defaultServiceTaskDelegate";
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnectorDiscovery? _connectorDiscovery;
    private readonly IExpressionManager _expressionManager;
    private readonly IEventDispatcher? _eventDispatcher;
    private readonly IJobManager? _jobManager;
    private readonly IProcessDefinitionManager? _processDefinitionManager;
    private readonly IRuntimeService? _runtimeService;

    public DefaultActivityBehaviorFactory(
        IServiceProvider serviceProvider,
        IExpressionManager expressionManager,
        IConnectorDiscovery? connectorDiscovery = null,
        IEventDispatcher? eventDispatcher = null,
        IJobManager? jobManager = null,
        IProcessDefinitionManager? processDefinitionManager = null,
        IRuntimeService? runtimeService = null)
    {
        _serviceProvider = serviceProvider;
        _connectorDiscovery = connectorDiscovery;
        _expressionManager = expressionManager;
        _eventDispatcher = eventDispatcher;
        _jobManager = jobManager;
        _processDefinitionManager = processDefinitionManager;
        _runtimeService = runtimeService;
    }

    public IBpmnActivityBehavior CreateServiceTaskBehavior(BpmnModelNs.ServiceTask serviceTask)
    {
        if (!string.IsNullOrWhiteSpace(serviceTask.Type))
        {
            if (string.Equals(serviceTask.Type, "mail", StringComparison.OrdinalIgnoreCase))
            {
                return CreateMailTaskBehavior(serviceTask);
            }

            if (string.Equals(serviceTask.Type, "webService", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(serviceTask.Type, "webservice", StringComparison.OrdinalIgnoreCase))
            {
                return CreateWebServiceTaskBehavior(serviceTask);
            }

            if (string.Equals(serviceTask.Type, "shell", StringComparison.OrdinalIgnoreCase))
            {
                return new ShellActivityBehavior(_expressionManager);
            }
        }

        if (!string.IsNullOrWhiteSpace(serviceTask.Implementation))
        {
            var delegateInstance = ResolveDelegate(serviceTask.Implementation);
            if (delegateInstance != null)
            {
                return new ServiceTaskDelegateActivityBehavior(delegateInstance);
            }
        }

        if (!string.IsNullOrEmpty(serviceTask.Implementation) && _connectorDiscovery != null)
        {
            var connector = _connectorDiscovery.GetConnector(serviceTask.Implementation);
            if (connector != null)
            {
                return new ConnectorActivityBehavior(connector);
            }
        }

        if (!string.IsNullOrEmpty(serviceTask.Class))
        {
            var delegateInstance = ResolveDelegate(serviceTask.Class);
            if (delegateInstance != null)
            {
                return new ServiceTaskDelegateActivityBehavior(delegateInstance);
            }
        }

        if (!string.IsNullOrEmpty(serviceTask.DelegateExpression))
        {
            var delegateExpression = ResolveDelegateExpression(serviceTask.DelegateExpression);
            if (delegateExpression != null)
            {
                return new ServiceTaskDelegateExpressionActivityBehavior(delegateExpression);
            }
        }

        if (!string.IsNullOrEmpty(serviceTask.Expression))
        {
            var expression = _expressionManager.CreateExpression(serviceTask.Expression);
            return new ServiceTaskExpressionActivityBehavior(expression, serviceTask.ResultVariableName);
        }

        if (!string.IsNullOrWhiteSpace(serviceTask.Implementation))
        {
            var delegateExpression = ResolveDelegateExpression(serviceTask.Implementation);
            if (delegateExpression != null)
            {
                return new ServiceTaskDelegateExpressionActivityBehavior(delegateExpression);
            }
        }

        var defaultServiceTaskDelegate = ResolveDefaultServiceTaskDelegate();
        if (defaultServiceTaskDelegate != null)
        {
            return new ServiceTaskDelegateExpressionActivityBehavior(defaultServiceTaskDelegate);
        }

        return new ServiceTaskActivityBehavior();
    }

    public IBpmnActivityBehavior CreateUserTaskBehavior(BpmnModelNs.UserTask userTask)
    {
        return new UserTaskActivityBehavior(
            userTask,
            _expressionManager,
            ResolveEventDispatcher(),
            new ListenerNotificationHelper(_expressionManager, _serviceProvider));
    }

    public IBpmnActivityBehavior CreateExclusiveGatewayBehavior(BpmnModelNs.ExclusiveGateway gateway)
    {
        return new ExclusiveGatewayActivityBehavior(_expressionManager);
    }

    public IBpmnActivityBehavior CreateParallelGatewayBehavior(BpmnModelNs.ParallelGateway gateway)
    {
        return new ParallelGatewayActivityBehavior();
    }

    public IBpmnActivityBehavior CreateInclusiveGatewayBehavior(BpmnModelNs.InclusiveGateway gateway)
    {
        return new InclusiveGatewayActivityBehavior(_expressionManager);
    }

    public IBpmnActivityBehavior CreateSubProcessBehavior(BpmnModelNs.SubProcess subProcess)
    {
        if (subProcess is BpmnModelNs.Transaction)
        {
            return new TransactionSubProcessActivityBehavior(subProcess, _expressionManager);
        }

        if (subProcess.TriggeredByEvent)
        {
            return new EventSubProcessActivityBehavior(subProcess, _expressionManager);
        }

        return new EmbeddedSubProcessActivityBehavior(subProcess, _expressionManager);
    }

    public IBpmnActivityBehavior CreateCallActivityBehavior(BpmnModelNs.CallActivity callActivity)
    {
        var behavior = new CallActivityBehavior(
            callActivity.CalledElement,
            _expressionManager,
            ResolveProcessDefinitionManager(),
            ResolveRuntimeService());
        behavior.InheritVariables = callActivity.InheritVariables;
        behavior.InheritBusinessKey = callActivity.InheritBusinessKey;
        behavior.BusinessKey = callActivity.BusinessKey;
        return behavior;
    }

    public IBpmnActivityBehavior CreateScriptTaskBehavior(BpmnModelNs.ScriptTask scriptTask)
    {
        return new ScriptTaskActivityBehavior
        {
            Script = scriptTask.Script,
            ScriptFormat = scriptTask.ScriptFormat,
            AutoStoreVariables = scriptTask.AutoStoreVariables,
            ResultVariable = scriptTask.ResultVariable,
            ExpressionManager = _expressionManager
        };
    }

    public IBpmnActivityBehavior CreateReceiveTaskBehavior(BpmnModelNs.ReceiveTask receiveTask)
    {
        return new ReceiveTaskActivityBehavior();
    }

    public IBpmnActivityBehavior CreateManualTaskBehavior(BpmnModelNs.ManualTask manualTask)
    {
        return new ManualTaskActivityBehavior();
    }

    public IBpmnActivityBehavior CreateBusinessRuleTaskBehavior(BpmnModelNs.BusinessRuleTask businessRuleTask)
    {
        return new BusinessRuleTaskActivityBehavior(
            businessRuleTask.RuleVariablesInput,
            businessRuleTask.Rules,
            businessRuleTask.ResultVariable,
            businessRuleTask.Exclude,
            _expressionManager);
    }

    public IBpmnActivityBehavior CreateStartEventBehavior(BpmnModelNs.StartEvent startEvent)
    {
        var eventSubProcess = startEvent.ParentContainer as BpmnModelNs.SubProcess;
        var isEventSubProcessStart = eventSubProcess?.TriggeredByEvent == true;

        if (startEvent.EventDefinitions != null)
        {
            var eventDef = startEvent.EventDefinitions.FirstOrDefault();
            if (eventDef is BpmnModelNs.SignalEventDefinition signalDef)
            {
                return new SignalStartEventActivityBehavior(signalDef, null, _expressionManager);
            }
            if (eventDef is BpmnModelNs.MessageEventDefinition messageDef)
            {
                if (isEventSubProcessStart)
                {
                    return new EventSubProcessMessageStartEventActivityBehavior(messageDef);
                }

                return new MessageStartEventActivityBehavior(
                    messageDef,
                    null,
                    _expressionManager,
                    ResolveEventDispatcher());
            }
            if (eventDef is BpmnModelNs.TimerEventDefinition timerDef)
            {
                return new TimerStartEventActivityBehavior(
                    timerDef,
                    _expressionManager,
                    ResolveJobManager());
            }
            if (eventDef is BpmnModelNs.ErrorEventDefinition errorDef)
            {
                if (isEventSubProcessStart)
                {
                    return new EventSubProcessErrorStartEventActivityBehavior(errorDef.ErrorCode);
                }

                return new ErrorStartEventActivityBehavior(errorDef.ErrorCode);
            }
            if (eventDef is BpmnModelNs.ConditionalEventDefinition conditionalDef)
            {
                return new ConditionalIntermediateCatchEventActivityBehavior(
                    conditionalDef.ConditionExpression ?? conditionalDef.Condition,
                    _expressionManager);
            }
            if (eventDef is BpmnModelNs.EscalationEventDefinition escalationDef)
            {
                return new EscalationStartEventActivityBehavior(escalationDef.EscalationRef, escalationDef.EscalationCode);
            }
        }

        return new NoneStartEventActivityBehavior(_expressionManager);
    }

    public IBpmnActivityBehavior CreateEndEventBehavior(BpmnModelNs.EndEvent endEvent)
    {
        if (endEvent.EventDefinitions != null)
        {
            var eventDef = endEvent.EventDefinitions.FirstOrDefault();
            if (eventDef is BpmnModelNs.ErrorEventDefinition errorDef)
            {
                return new ErrorEndEventActivityBehavior(errorDef.ErrorCode);
            }
            if (eventDef is BpmnModelNs.CancelEventDefinition)
            {
                return new CancelEndEventActivityBehavior();
            }
            if (eventDef is BpmnModelNs.TerminateEventDefinition terminateDef)
            {
                return new TerminateEndEventActivityBehavior(
                    ResolveEventDispatcher(),
                    terminateDef.TerminateAll,
                    terminateDef.TerminateMultiInstance);
            }
            if (eventDef is BpmnModelNs.EscalationEventDefinition escalationDef)
            {
                return new EscalationEndEventActivityBehavior(escalationDef.EscalationRef, escalationDef.EscalationCode);
            }
        }

        return new EndEventActivityBehavior();
    }

    public IBpmnActivityBehavior CreateBoundaryEventBehavior(BpmnModelNs.BoundaryEvent boundaryEvent)
    {
        if (boundaryEvent.EventDefinitions != null)
        {
            var eventDef = boundaryEvent.EventDefinitions.FirstOrDefault();
            if (eventDef is BpmnModelNs.SignalEventDefinition signalDef)
            {
                return new SignalBoundaryEventActivityBehavior(signalDef, null, boundaryEvent.CancelActivity, _expressionManager);
            }
            if (eventDef is BpmnModelNs.MessageEventDefinition messageDef)
            {
                return new MessageBoundaryEventActivityBehavior(messageDef, boundaryEvent.CancelActivity);
            }
            if (eventDef is BpmnModelNs.ErrorEventDefinition errorDef)
            {
                return new ErrorBoundaryEventActivityBehavior(errorDef.ErrorCode, boundaryEvent.CancelActivity);
            }
            if (eventDef is BpmnModelNs.CancelEventDefinition)
            {
                return new BoundaryCancelEventActivityBehavior(new BpmnModelNs.CancelEventDefinition(), boundaryEvent.CancelActivity);
            }
            if (eventDef is BpmnModelNs.CompensateEventDefinition compensateDef)
            {
                return new CompensateBoundaryEventActivityBehavior(compensateDef, boundaryEvent.CancelActivity);
            }
            if (eventDef is BpmnModelNs.TimerEventDefinition timerDef)
            {
                return new TimerBoundaryEventActivityBehavior(
                    timerDef,
                    boundaryEvent.CancelActivity,
                    _expressionManager,
                    ResolveJobManager());
            }
            if (eventDef is BpmnModelNs.ConditionalEventDefinition conditionalDef)
            {
                return new ConditionalBoundaryEventActivityBehavior(
                    conditionalDef.ConditionExpression ?? conditionalDef.Condition,
                    boundaryEvent.CancelActivity,
                    _expressionManager);
            }
            if (eventDef is BpmnModelNs.EscalationEventDefinition escalationDef)
            {
                return new EscalationBoundaryEventActivityBehavior(
                    escalationDef.EscalationRef,
                    escalationDef.EscalationCode,
                    boundaryEvent.CancelActivity);
            }
        }

        return new BoundaryEventActivityBehavior(boundaryEvent.CancelActivity);
    }

    public IBpmnActivityBehavior CreateIntermediateCatchEventBehavior(BpmnModelNs.IntermediateCatchEvent catchEvent)
    {
        if (catchEvent.EventDefinitions != null)
        {
            var eventDef = catchEvent.EventDefinitions.FirstOrDefault();
            if (eventDef is BpmnModelNs.SignalEventDefinition signalDef)
            {
                return new IntermediateSignalCatchEventActivityBehavior(signalDef, null, _expressionManager);
            }
            if (eventDef is BpmnModelNs.MessageEventDefinition messageDef)
            {
                return new IntermediateMessageCatchEventActivityBehavior(messageDef);
            }
            if (eventDef is BpmnModelNs.TimerEventDefinition timerDef)
            {
                return new IntermediateCatchTimerEventActivityBehavior(
                    timerDef,
                    _expressionManager,
                    ResolveJobManager());
            }
            if (eventDef is BpmnModelNs.LinkEventDefinition linkDef)
            {
                return new IntermediateCatchLinkEventActivityBehavior(linkDef);
            }
            if (eventDef is BpmnModelNs.ConditionalEventDefinition conditionalDef)
            {
                return new ConditionalIntermediateCatchEventActivityBehavior(
                    conditionalDef.ConditionExpression ?? conditionalDef.Condition,
                    _expressionManager);
            }
        }

        return new IntermediateCatchEventActivityBehavior();
    }

    public IBpmnActivityBehavior CreateIntermediateThrowEventBehavior(BpmnModelNs.IntermediateThrowEvent throwEvent)
    {
        if (throwEvent.EventDefinitions != null)
        {
            var eventDef = throwEvent.EventDefinitions.FirstOrDefault();
            if (eventDef is BpmnModelNs.SignalEventDefinition signalDef)
            {
                return new SignalThrowEventActivityBehavior(signalDef, null, _expressionManager);
            }
            if (eventDef is BpmnModelNs.MessageEventDefinition messageDef)
            {
                return new IntermediateThrowMessageEventActivityBehavior(messageDef, null, _expressionManager);
            }
            if (eventDef is BpmnModelNs.LinkEventDefinition)
            {
                return new IntermediateThrowLinkEventActivityBehavior(throwEvent);
            }
            if (eventDef is BpmnModelNs.EscalationEventDefinition escalationDef)
            {
                return new EscalationThrowEventActivityBehavior(escalationDef.EscalationRef, escalationDef.EscalationCode);
            }
        }

        return new IntermediateThrowEventActivityBehavior();
    }

    public IBpmnActivityBehavior CreateMultiInstanceActivityBehavior(BpmnModelNs.Activity activity, IBpmnActivityBehavior innerBehavior)
    {
        if (activity.LoopCharacteristics == null)
        {
            return innerBehavior;
        }

        MultiInstanceActivityBehavior behavior;
        if (activity.LoopCharacteristics.IsSequential)
        {
            behavior = new SequentialMultiInstanceActivityBehavior(activity, innerBehavior, _expressionManager);
        }
        else
        {
            behavior = new ParallelMultiInstanceActivityBehavior(activity, innerBehavior, _expressionManager);
        }

        behavior.LoopCardinalityExpression = activity.LoopCharacteristics.LoopCardinality;
        behavior.CompletionConditionExpression = activity.LoopCharacteristics.CompletionCondition;
        behavior.CollectionExpression = activity.LoopCharacteristics.Collection;
        behavior.CollectionVariable = activity.LoopCharacteristics.CollectionVariable;
        behavior.CollectionElementVariable = activity.LoopCharacteristics.ElementVariable;
        behavior.CollectionElementIndexVariable = activity.LoopCharacteristics.ElementIndexVariable ?? behavior.CollectionElementIndexVariable;
        behavior.OutputDataItem = activity.LoopCharacteristics.OutputDataItem;
        return behavior;
    }

    public IBpmnActivityBehavior CreateMailTaskBehavior(BpmnModelNs.ServiceTask serviceTask)
    {
        return new MailActivityBehavior(_expressionManager);
    }

    public IBpmnActivityBehavior CreateWebServiceTaskBehavior(BpmnModelNs.ServiceTask serviceTask)
    {
        return new WebServiceActivityBehavior(_expressionManager);
    }

    private IEventDispatcher? ResolveEventDispatcher()
    {
        return _eventDispatcher ?? _serviceProvider.GetService(typeof(IEventDispatcher)) as IEventDispatcher;
    }

    private IJobManager? ResolveJobManager()
    {
        return _jobManager ?? _serviceProvider.GetService(typeof(IJobManager)) as IJobManager;
    }

    private IProcessDefinitionManager? ResolveProcessDefinitionManager()
    {
        return _processDefinitionManager ?? _serviceProvider.GetService(typeof(IProcessDefinitionManager)) as IProcessDefinitionManager;
    }

    private IRuntimeService? ResolveRuntimeService()
    {
        return _runtimeService ?? _serviceProvider.GetService(typeof(IRuntimeService)) as IRuntimeService;
    }

    private IWorkflowDelegate? ResolveDelegate(string className)
    {
        var type = Type.GetType(className);
        if (type == null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(className);
                if (type != null) break;
            }
        }

        if (type == null) return null;

        var service = _serviceProvider.GetService(type);
        if (service is IWorkflowDelegate workflowDelegate) return workflowDelegate;

        try
        {
            var instance = Activator.CreateInstance(type);
            if (instance is IWorkflowDelegate createdDelegate) return createdDelegate;
        }
        catch
        {
        }

        return null;
    }

    private IDelegateExpression? ResolveDelegateExpression(string delegateExpressionText)
    {
        var expression = delegateExpressionText.Trim();
        if (expression.StartsWith("${") && expression.EndsWith("}"))
        {
            expression = expression[2..^1];
        }
        else if (expression.StartsWith("#{") && expression.EndsWith("}"))
        {
            expression = expression[2..^1];
        }

        try
        {
            var result = _expressionManager.Evaluate(expression, new());
            if (result is IDelegateExpression delegateExpression)
            {
                return delegateExpression;
            }
        }
        catch
        {
            // Ignore expression parsing failures and continue with type/DI resolution.
        }

        if (string.Equals(expression, DefaultServiceTaskBeanName, StringComparison.Ordinal))
        {
            var byInterface = _serviceProvider.GetService(typeof(IDelegateExpression)) as IDelegateExpression;
            if (byInterface != null)
            {
                return byInterface;
            }
        }

        var type = Type.GetType(expression);
        if (type == null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(expression);
                if (type != null) break;
            }
        }

        if (type != null)
        {
            var service = _serviceProvider.GetService(type);
            if (service is IDelegateExpression serviceDelegateExpression) return serviceDelegateExpression;

            try
            {
                var instance = Activator.CreateInstance(type);
                if (instance is IDelegateExpression createdDelegateExpression) return createdDelegateExpression;
            }
            catch
            {
            }
        }

        return null;
    }

    private IDelegateExpression? ResolveDefaultServiceTaskDelegate()
    {
        return ResolveDelegateExpression("${" + DefaultServiceTaskBeanName + "}");
    }
}
