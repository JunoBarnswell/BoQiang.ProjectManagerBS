using System.Collections.Concurrent;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 将同一租户、应用、项目的 WIP 判定与写入串行化。
/// 进程内信号量避免同一 API 实例的竞争；维护锁覆盖多实例场景，租约仅包围短事务。
/// </summary>
public sealed class ProjectManagementWipCoordinator(IProjectManagementMaintenanceLock? maintenanceLock = null)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

    public async ValueTask<IAsyncDisposable> EnterAsync(string tenantId, string appCode, string projectId, CancellationToken cancellationToken = default)
    {
        var key = string.Concat(tenantId.Trim(), ":", appCode.Trim().ToUpperInvariant(), ":", projectId.Trim());
        var gate = Gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        string? operationId = null;
        try
        {
            if (maintenanceLock is not null)
                operationId = await maintenanceLock.AcquireAsync($"project-management-wip:{projectId.Trim()}", LeaseDuration, cancellationToken);
            return new WipLease(gate, maintenanceLock, operationId);
        }
        catch
        {
            gate.Release();
            throw;
        }
    }

    private sealed class WipLease(SemaphoreSlim gate, IProjectManagementMaintenanceLock? maintenanceLock, string? operationId) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(operationId) && maintenanceLock is not null)
                    await maintenanceLock.ReleaseAsync(operationId, CancellationToken.None);
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
