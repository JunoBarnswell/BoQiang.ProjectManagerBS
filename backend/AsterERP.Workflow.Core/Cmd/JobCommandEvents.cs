using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Job;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

internal static class JobCommandEvents
{
    public static void DispatchEntityEvent(ICommandContext context, WorkflowEventType eventType, object entity)
    {
        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher == null || !eventDispatcher.IsEnabled)
            return;

        eventDispatcher.DispatchEvent(WorkflowEventBuilder.CreateEntityEvent(eventType, entity));
    }
}

