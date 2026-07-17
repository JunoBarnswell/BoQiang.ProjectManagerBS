using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Job;
using Microsoft.Extensions.Hosting;

namespace AsterERP.Workflow.DependencyInjection;

public sealed class AsyncExecutorOptions
{
    public int TimerJobAcquireWaitTimeInMillis { get; set; } = 5 * 1000;
    public int AsyncJobAcquireWaitTimeInMillis { get; set; } = 5 * 1000;
    public int MaxAsyncJobsDuePerAcquisition { get; set; } = 1;
    public int MaxTimerJobsPerAcquisition { get; set; } = 1;
    public int ResetExpiredJobsInterval { get; set; } = 60 * 1000;
    public int TimerLockTimeInMillis { get; set; } = 5 * 60 * 1000;
    public int AsyncJobLockTimeInMillis { get; set; } = 5 * 60 * 1000;
}

public sealed class HostedAsyncExecutor : IHostedService
{
    private readonly IProcessEngineConfiguration _configuration;
    private readonly IJobManager _jobManager;
    private readonly IEventDispatcher? _eventDispatcher;
    private readonly AsyncExecutorOptions _options;
    private DefaultAsyncJobExecutor? _executor;

    public HostedAsyncExecutor(
        IProcessEngineConfiguration configuration,
        IJobManager jobManager,
        IEventDispatcher? eventDispatcher = null,
        AsyncExecutorOptions? options = null)
    {
        _configuration = configuration;
        _jobManager = jobManager;
        _eventDispatcher = eventDispatcher;
        _options = options ?? new AsyncExecutorOptions();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _executor = new DefaultAsyncJobExecutor(_jobManager, _eventDispatcher, _configuration)
        {
            IsAutoActivate = true,
            DefaultTimerJobAcquireWaitTimeInMillis = _options.TimerJobAcquireWaitTimeInMillis,
            DefaultAsyncJobAcquireWaitTimeInMillis = _options.AsyncJobAcquireWaitTimeInMillis,
            MaxAsyncJobsDuePerAcquisition = _options.MaxAsyncJobsDuePerAcquisition,
            MaxTimerJobsPerAcquisition = _options.MaxTimerJobsPerAcquisition,
            ResetExpiredJobsInterval = _options.ResetExpiredJobsInterval,
            TimerLockTimeInMillis = _options.TimerLockTimeInMillis,
            AsyncJobLockTimeInMillis = _options.AsyncJobLockTimeInMillis
        };

        return _executor.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executor != null)
        {
            await _executor.StopAsync(cancellationToken);
            _executor.Dispose();
            _executor = null;
        }
    }
}
