namespace AsterERP.Workflow.Core.Management;

public interface IAsyncJobExecutor
{
    bool IsActive { get; }
    int CorePoolSize { get; set; }
    int MaxPoolSize { get; set; }
    int QueueCapacity { get; set; }
    global::System.Threading.Tasks.Task StartAsync(CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task StopAsync(CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task ExecuteAsyncJobAsync(string jobId, CancellationToken cancellationToken = default);
}
