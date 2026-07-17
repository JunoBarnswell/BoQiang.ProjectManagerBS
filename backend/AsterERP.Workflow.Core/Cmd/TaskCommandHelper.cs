using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

internal static class TaskCommandHelper
{
    public static TaskImplementation UpdateTask(
        ICommandContext context,
        string taskId,
        Func<TaskImplementation, TaskImplementation> update) =>
        throw new NotSupportedException("Synchronous task updates are not supported. Use UpdateTaskAsync.");

    public static async Task<TaskImplementation> UpdateTaskAsync(
        ICommandContext context,
        string taskId,
        Func<TaskImplementation, TaskImplementation> update,
        CancellationToken cancellationToken)
    {
        var execution = await context.FindExecutionByTaskIdAsync(taskId, cancellationToken);
        if (execution != null)
        {
            var index = execution.TaskEntities.FindIndex(task => task.Id == taskId);
            if (index < 0)
            {
                throw new WorkflowEngineObjectNotFoundException(
                    $"Cannot find task with id '{taskId}'", typeof(TaskImplementation));
            }

            var updatedInExecution = update(execution.TaskEntities[index]);
            execution.TaskEntities[index] = updatedInExecution;
            return updatedInExecution;
        }

        var standaloneTask = await context.GetTaskAsync(taskId, cancellationToken);
        if (standaloneTask == null)
        {
            throw new WorkflowEngineObjectNotFoundException(
                $"Cannot find task with id '{taskId}'", typeof(TaskImplementation));
        }

        var updatedStandaloneTask = update(standaloneTask);
        context.SaveTask(updatedStandaloneTask);
        return updatedStandaloneTask;
    }
}
