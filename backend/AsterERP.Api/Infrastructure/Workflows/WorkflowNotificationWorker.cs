using System;
using System.Threading;
using System.Threading.Tasks;

using AsterERP.Api.Application.Workflows;
using AsterERP.Api.Infrastructure.Abp.Settings;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.Settings;

namespace AsterERP.Api.Infrastructure.Workflows;

[DisableConcurrentExecution(30)]
public sealed class WorkflowNotificationWorker
{
    public WorkflowNotificationWorker(
        IServiceScopeFactory serviceScopeFactory,
        ISettingProvider settingProvider,
        ILogger<WorkflowNotificationWorker> logger)
    {
        ServiceScopeFactory = serviceScopeFactory;
        SettingProvider = settingProvider;
        Logger = logger;
    }

    private IServiceScopeFactory ServiceScopeFactory { get; }

    private ISettingProvider SettingProvider { get; }

    private ILogger<WorkflowNotificationWorker> Logger { get; }

    public async Task ProcessAsync()
    {
        try
        {
            if (!await IsWorkflowNotificationWorkerEnabledAsync())
            {
                return;
            }

            using var scope = ServiceScopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IWorkflowNotificationAppService>();
            var processed = await service.ProcessDueTasksAsync(50, CancellationToken.None);

            if (processed > 0)
            {
                Logger.LogInformation("Processed {Count} workflow notification tasks", processed);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Workflow notification worker failed");
        }
    }

    private async Task<bool> IsWorkflowNotificationWorkerEnabledAsync()
    {
        var value = await SettingProvider.GetOrNullAsync(AsterErpSettingNames.JobsAbpBackgroundJobsEnabled);
        return !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }
}
