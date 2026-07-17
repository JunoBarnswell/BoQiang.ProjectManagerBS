using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ApplicationConsole;
using AsterERP.Contracts.Auth;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationLoginBootstrapCacheTests
{
    [Fact]
    public async Task Concurrent_requests_for_the_same_scope_share_one_probe()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateCache(memoryCache);
        var response = CreateResponse(canManage: false);
        var gate = new TaskCompletionSource<ApplicationLoginBootstrapResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCalls = 0;

        var requests = Enumerable.Range(0, 12)
            .Select(_ => cache.GetOrCreateAsync(
                "tenant-a",
                "WMS",
                canManageInitialBinding: false,
                _ =>
                {
                    Interlocked.Increment(ref factoryCalls);
                    return gate.Task;
                }))
            .ToArray();

        Assert.Equal(1, Volatile.Read(ref factoryCalls));
        gate.SetResult(response);
        var results = await Task.WhenAll(requests);

        Assert.All(results, item => Assert.Same(response, item));
        Assert.Equal(1, factoryCalls);

        var cached = await cache.GetOrCreateAsync(
            "tenant-a",
            "WMS",
            canManageInitialBinding: false,
            _ => throw new InvalidOperationException("Cached bootstrap unexpectedly probed the database."));
        Assert.Same(response, cached);
    }

    [Fact]
    public async Task Public_and_platform_admin_scopes_are_isolated()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateCache(memoryCache);
        var factoryCalls = 0;

        var publicResponse = await cache.GetOrCreateAsync(
            "tenant-a",
            "MES",
            canManageInitialBinding: false,
            _ =>
            {
                Interlocked.Increment(ref factoryCalls);
                return Task.FromResult(CreateResponse(canManage: false));
            });
        var adminResponse = await cache.GetOrCreateAsync(
            "tenant-a",
            "MES",
            canManageInitialBinding: true,
            _ =>
            {
                Interlocked.Increment(ref factoryCalls);
                return Task.FromResult(CreateResponse(canManage: true));
            });

        Assert.False(publicResponse.DatabaseBinding.CanManage);
        Assert.True(adminResponse.DatabaseBinding.CanManage);
        Assert.Equal(2, factoryCalls);
    }

    [Fact]
    public async Task Removing_a_binding_key_invalidates_both_scopes()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateCache(memoryCache);
        var factoryCalls = 0;

        await cache.GetOrCreateAsync(
            "tenant-a",
            "P0",
            false,
            _ =>
            {
                Interlocked.Increment(ref factoryCalls);
                return Task.FromResult(CreateResponse(false));
            });
        await cache.GetOrCreateAsync(
            "tenant-a",
            "P0",
            true,
            _ =>
            {
                Interlocked.Increment(ref factoryCalls);
                return Task.FromResult(CreateResponse(true));
            });

        cache.Remove("tenant-a", "P0");

        await cache.GetOrCreateAsync(
            "tenant-a",
            "P0",
            false,
            _ =>
            {
                Interlocked.Increment(ref factoryCalls);
                return Task.FromResult(CreateResponse(false));
            });
        await cache.GetOrCreateAsync(
            "tenant-a",
            "P0",
            true,
            _ =>
            {
                Interlocked.Increment(ref factoryCalls);
                return Task.FromResult(CreateResponse(true));
            });

        Assert.Equal(4, factoryCalls);
    }

    [Fact]
    public async Task In_flight_probe_from_an_old_generation_cannot_repopulate_current_cache()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateCache(memoryCache);
        var oldResponse = CreateResponse(canManage: false);
        var currentResponse = CreateResponse(canManage: true);
        var oldProbe = new TaskCompletionSource<ApplicationLoginBootstrapResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCalls = 0;

        var staleTask = cache.GetOrCreateAsync(
            "tenant-a",
            "WMS",
            false,
            _ =>
            {
                Interlocked.Increment(ref factoryCalls);
                return oldProbe.Task;
            });
        cache.Remove("tenant-a", "WMS");

        var fresh = await cache.GetOrCreateAsync(
            "tenant-a",
            "WMS",
            false,
            _ =>
            {
                Interlocked.Increment(ref factoryCalls);
                return Task.FromResult(currentResponse);
            });
        oldProbe.SetResult(oldResponse);
        await staleTask;

        var stillFresh = await cache.GetOrCreateAsync(
            "tenant-a",
            "WMS",
            false,
            _ => throw new InvalidOperationException("Old generation replaced the current cache entry."));

        Assert.Same(currentResponse, fresh);
        Assert.Same(currentResponse, stillFresh);
        Assert.Equal(2, factoryCalls);
    }

    [Fact]
    public void Cache_TTL_has_a_short_bounded_configuration()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        Assert.Equal(TimeSpan.FromSeconds(15), CreateCache(memoryCache).ResolveTtl());

        var low = CreateCache(memoryCache, "0");
        var high = CreateCache(memoryCache, "600");
        Assert.Equal(TimeSpan.FromSeconds(1), low.ResolveTtl());
        Assert.Equal(TimeSpan.FromSeconds(60), high.ResolveTtl());
    }

    private static ApplicationLoginBootstrapCache CreateCache(
        IMemoryCache memoryCache,
        string? ttlSeconds = null)
    {
        var values = new Dictionary<string, string?>();
        if (ttlSeconds is not null)
        {
            values["Auth:ApplicationBootstrapCacheSeconds"] = ttlSeconds;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new ApplicationLoginBootstrapCache(memoryCache, configuration);
    }

    private static ApplicationLoginBootstrapResponse CreateResponse(bool canManage) =>
        new(
            "tenant-a",
            "Tenant A",
            "WMS",
            "Warehouse",
            "Tenant A WMS",
            "Enabled",
            new ApplicationDatabaseBindingStatusResponse(
                IsBound: true,
                IsReachable: true,
                Provider: null,
                DisplayName: null,
                DatabaseName: null,
                UpdatedAt: null,
                CanManage: canManage,
                Message: null));
}
