namespace AsterERP.Workflow.Core.Job;

public class JobRetryStrategy
{
    public int DefaultRetries { get; set; } = 3;
    public List<TimeSpan> RetryIntervals { get; set; } = new()
    {
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30)
    };

    public TimeSpan? GetNextRetryDelay(int currentRetryCount)
    {
        if (currentRetryCount <= 0 || currentRetryCount > RetryIntervals.Count)
            return null;
        return RetryIntervals[currentRetryCount - 1];
    }

    public DateTime? CalculateNextDueDate(int currentRetryCount)
    {
        var delay = GetNextRetryDelay(currentRetryCount);
        return delay.HasValue ? AbpTimeIdProvider.UtcNow.Add(delay.Value) : null;
    }
}

