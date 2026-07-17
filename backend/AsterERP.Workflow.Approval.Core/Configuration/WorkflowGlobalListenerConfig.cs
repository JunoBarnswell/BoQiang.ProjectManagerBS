using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Approval.Core.Listeners.Global;
using Microsoft.Extensions.DependencyInjection;

namespace AsterERP.Workflow.Approval.Core.Configuration;

public static class WorkflowGlobalListenerConfig
{
    public static IServiceCollection AddAsterERPWorkflowGlobalListeners(this IServiceCollection services)
    {
        services.AddSingleton<GlobalProcessStartListener>();
        services.AddSingleton<GlobalProcessEndListener>();
        return services;
    }

    public static void RegisterGlobalListeners(IServiceProvider serviceProvider)
    {
        var dispatcher = serviceProvider.GetRequiredService<IEventDispatcher>();

        var startListener = serviceProvider.GetRequiredService<GlobalProcessStartListener>();
        var endListener = serviceProvider.GetRequiredService<GlobalProcessEndListener>();

        dispatcher.AddEventListener(startListener, WorkflowEventType.PROCESS_STARTED);
        dispatcher.AddEventListener(endListener, WorkflowEventType.PROCESS_COMPLETED);
    }
}
