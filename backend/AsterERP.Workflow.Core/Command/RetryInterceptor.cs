using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Engine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AsterERP.Workflow.Core.Command;

public class RetryInterceptor : AbstractCommandInterceptor
{
    private readonly ILogger<RetryInterceptor> _logger;

    public int NumOfRetries { get; set; } = 3;
    public int WaitTimeInMs { get; set; } = 50;
    public int WaitIncreaseFactor { get; set; } = 5;

    public RetryInterceptor()
    {
        _logger = NullLogger<RetryInterceptor>.Instance;
    }

    public RetryInterceptor(ILogger<RetryInterceptor> logger)
    {
        _logger = logger;
    }

    public override T Execute<T>(ICommand<T> command, Func<ICommand<T>, T> next)
    {
        throw new NotSupportedException("RetryInterceptor is async-only. Use ExecuteAsync.");
    }

    public override async Task<T> ExecuteAsync<T>(
        ICommand<T> command,
        Func<ICommand<T>, Task<T>> next,
        CancellationToken cancellationToken = default)
    {
        long waitTime = WaitTimeInMs;
        int failedAttempts = 0;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (failedAttempts > 0)
            {
                _logger.LogInformation("Waiting for {WaitTime}ms before retrying the command.", waitTime);
                await Task.Delay(GetRetryDelayMilliseconds(waitTime), cancellationToken);
                waitTime = GetNextWaitTime(waitTime);
            }

            try
            {
                return await next(command);
            }
            catch (AsterERP.Workflow.Common.WorkflowEngineOptimisticLockingException ex) when (ShouldRetry(command.GetType(), ex))
            {
                _logger.LogInformation("Caught optimistic locking exception: {Message}", ex.Message);
            }

            failedAttempts++;
        } while (failedAttempts <= NumOfRetries);

        throw new AsterERP.Workflow.Common.WorkflowEngineException(
            $"{NumOfRetries} retries failed with WorkflowEngineOptimisticLockingException. Giving up.");
    }

    private int GetRetryDelayMilliseconds(long waitTime)
    {
        return checked((int)Math.Min(int.MaxValue, Math.Max(0, waitTime)));
    }

    private long GetNextWaitTime(long waitTime)
    {
        if (WaitIncreaseFactor <= 1 || waitTime >= int.MaxValue)
        {
            return Math.Min(int.MaxValue, waitTime);
        }

        return Math.Min(int.MaxValue, waitTime * (long)WaitIncreaseFactor);
    }

    private static bool ShouldRetry(Type commandType, AsterERP.Workflow.Common.WorkflowEngineOptimisticLockingException exception)
    {
        if (commandType.Name == "SetExecutionVariablesCmd")
        {
            return false;
        }

        return !exception.Message.Contains("database has revision", StringComparison.OrdinalIgnoreCase)
            && !exception.Message.Contains("concurrent update in progress", StringComparison.OrdinalIgnoreCase);
    }

}
