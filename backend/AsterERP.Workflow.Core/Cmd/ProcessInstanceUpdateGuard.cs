using System.Collections.Concurrent;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;

namespace AsterERP.Workflow.Core.Cmd;

internal static class ProcessInstanceUpdateGuard
{
    private static readonly ConcurrentDictionary<string, int> ActiveUpdates = new(StringComparer.Ordinal);

    public static void Enter(string processInstanceId)
    {
        var activeUpdates = ActiveUpdates.AddOrUpdate(processInstanceId, 1, static (_, count) => count + 1);
        if (activeUpdates > 1)
        {
            ActiveUpdates.AddOrUpdate(processInstanceId, 0, static (_, count) => Math.Max(0, count - 1));
            throw new WorkflowEngineOptimisticLockingException(
                $"Optimistic locking failed for process instance '{processInstanceId}': concurrent update in progress.");
        }
    }

    public static ICommandContextCloseListener CreateReleaseListener(string processInstanceId)
    {
        return new ReleaseListener(processInstanceId);
    }

    private sealed class ReleaseListener : ICommandContextCloseListener
    {
        private readonly string _processInstanceId;

        public ReleaseListener(string processInstanceId)
        {
            _processInstanceId = processInstanceId;
        }

        public void Closing(ICommandContext commandContext)
        {
        }

        public void AfterSessionsFlush(ICommandContext commandContext)
        {
        }

        public void Closed(ICommandContext commandContext)
        {
            Release();
        }

        public void CloseFailure(ICommandContext commandContext)
        {
            Release();
        }

        private void Release()
        {
            ActiveUpdates.AddOrUpdate(
                _processInstanceId,
                0,
                static (_, count) => Math.Max(0, count - 1));
        }
    }
}
