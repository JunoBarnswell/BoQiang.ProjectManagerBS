using System.Collections.Concurrent;
using AsterERP.Contracts.Im;
using Volo.Abp.Timing;

namespace AsterERP.Api.Application.Im;

public sealed class ImPresenceService(IClock clock) : IImPresenceService
{
    private readonly ConcurrentDictionary<string, int> connectionCounts = new(StringComparer.OrdinalIgnoreCase);

    public Task<ImPresenceChangedResponse?> ConnectedAsync(string tenantId, string appCode, string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = BuildKey(tenantId, appCode, userId);
        var count = connectionCounts.AddOrUpdate(key, 1, (_, current) => current + 1);
        return Task.FromResult(count == 1
            ? new ImPresenceChangedResponse(userId.Trim(), tenantId.Trim(), appCode.Trim().ToUpperInvariant(), true, clock.Now)
            : null);
    }

    public Task<ImPresenceChangedResponse?> DisconnectedAsync(string tenantId, string appCode, string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = BuildKey(tenantId, appCode, userId);
        var wentOffline = false;
        connectionCounts.AddOrUpdate(
            key,
            _ => 0,
            (_, current) =>
            {
                var next = Math.Max(current - 1, 0);
                wentOffline = next == 0;
                return next;
            });

        if (wentOffline)
        {
            connectionCounts.TryRemove(key, out _);
        }

        return Task.FromResult(wentOffline
            ? new ImPresenceChangedResponse(userId.Trim(), tenantId.Trim(), appCode.Trim().ToUpperInvariant(), false, clock.Now)
            : null);
    }

    public IReadOnlySet<string> GetOnlineUserIds(string tenantId, string appCode)
    {
        var prefix = $"{tenantId.Trim()}:{appCode.Trim().ToUpperInvariant()}:";
        return connectionCounts
            .Where(item => item.Value > 0 && item.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Key[prefix.Length..])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildKey(string tenantId, string appCode, string userId) =>
        $"{tenantId.Trim()}:{appCode.Trim().ToUpperInvariant()}:{userId.Trim()}";
}
