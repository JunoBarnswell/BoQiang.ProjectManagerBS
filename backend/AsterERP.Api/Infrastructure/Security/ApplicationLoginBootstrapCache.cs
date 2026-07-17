using System.Collections.Concurrent;
using AsterERP.Contracts.Auth;
using Microsoft.Extensions.Caching.Memory;

namespace AsterERP.Api.Infrastructure.Security;

public sealed class ApplicationLoginBootstrapCache(
    IMemoryCache memoryCache,
    IConfiguration configuration)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<ApplicationLoginBootstrapResponse>>> inFlight =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> generations =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<ApplicationLoginBootstrapResponse> GetOrCreateAsync(
        string tenantId,
        string appCode,
        bool canManageInitialBinding,
        Func<CancellationToken, Task<ApplicationLoginBootstrapResponse>> factory,
        CancellationToken cancellationToken = default)
    {
        var baseKey = BuildBaseKey(tenantId, appCode);
        var generation = generations.TryGetValue(baseKey, out var currentGeneration)
            ? currentGeneration
            : 0;
        var key = BuildScopedKey(baseKey, canManageInitialBinding, generation);
        if (memoryCache.TryGetValue<ApplicationLoginBootstrapResponse>(key, out var cached) && cached is not null)
        {
            return cached;
        }

        var candidate = new Lazy<Task<ApplicationLoginBootstrapResponse>>(
            () => LoadAndCacheAsync(key, factory),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var selected = inFlight.GetOrAdd(key, candidate);
        if (ReferenceEquals(selected, candidate))
        {
            _ = selected.Value.ContinueWith(
                completedTask =>
                {
                    _ = completedTask.Exception;
                    if (inFlight.TryGetValue(key, out var active) && ReferenceEquals(active, selected))
                    {
                        inFlight.TryRemove(key, out _);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        return await selected.Value.WaitAsync(cancellationToken);
    }

    public void Remove(string tenantId, string appCode)
    {
        var baseKey = BuildBaseKey(tenantId, appCode);
        var nextGeneration = generations.AddOrUpdate(baseKey, 1, (_, current) => current + 1);
        var previousGeneration = nextGeneration - 1;
        RemoveGeneration(baseKey, previousGeneration);
    }

    internal TimeSpan ResolveTtl()
    {
        var seconds = Math.Clamp(
            configuration.GetValue("Auth:ApplicationBootstrapCacheSeconds", 15),
            1,
            60);
        return TimeSpan.FromSeconds(seconds);
    }

    private async Task<ApplicationLoginBootstrapResponse> LoadAndCacheAsync(
        string key,
        Func<CancellationToken, Task<ApplicationLoginBootstrapResponse>> factory)
    {
        var response = await factory(CancellationToken.None);
        memoryCache.Set(key, response, ResolveTtl());
        return response;
    }

    private void RemoveGeneration(string baseKey, long generation)
    {
        foreach (var canManage in new[] { false, true })
        {
            var key = BuildScopedKey(baseKey, canManage, generation);
            memoryCache.Remove(key);
            inFlight.TryRemove(key, out _);
        }
    }

    private static string BuildBaseKey(string tenantId, string appCode) =>
        $"application-login-bootstrap:{tenantId.Trim().ToLowerInvariant()}:{appCode.Trim().ToUpperInvariant()}";

    private static string BuildScopedKey(
        string baseKey,
        bool canManageInitialBinding,
        long generation) =>
        $"{baseKey}:{(canManageInitialBinding ? "platform-admin" : "public")}:{generation}";
}
