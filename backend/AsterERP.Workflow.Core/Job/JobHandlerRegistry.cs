namespace AsterERP.Workflow.Core.Job;

internal static class JobHandlerRegistry
{
    public static IJobHandler? Resolve(string? handlerType)
    {
        return handlerType switch
        {
            AsyncContinuationJobHandler.HandlerType => new AsyncContinuationJobHandler(),
            TriggerTimerEventJobHandler.HandlerType => new TriggerTimerEventJobHandler(),
            "timer-intermediate-catch" => new TriggerTimerEventJobHandler(),
            "timer-transition" => new TriggerTimerEventJobHandler(),
            TimerStartEventJobHandler.HandlerType => new TimerStartEventJobHandler(),
            ProcessEventJobHandler.HandlerType => new ProcessEventJobHandler(),
            TimerSuspendProcessDefinitionHandler.HandlerType => new TimerSuspendProcessDefinitionHandler(),
            TimerActivateProcessDefinitionHandler.HandlerType => new TimerActivateProcessDefinitionHandler(),
            _ => null
        };
    }
}
