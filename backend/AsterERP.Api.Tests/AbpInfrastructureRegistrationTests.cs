using AsterERP.Api.Infrastructure.Abp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Medallion.Threading;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class AbpInfrastructureRegistrationTests
{
    [Fact]
    public void ProductionWithoutRedisOrExplicitFileLockFailsFastBeforeApplicationStarts()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production"
        });

        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddAsterErpAbpInfrastructure(configuration));

        Assert.Contains("生产环境必须配置", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProductionWithExplicitFileLockUsesFileSystemWithoutRedis()
    {
        var lockPath = Path.Combine(Path.GetTempPath(), "astererp-lock-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["DistributedLock:FilePath"] = lockPath
            });
            var services = new ServiceCollection();
            services.AddAsterErpAbpInfrastructure(configuration);
            await using var provider = services.BuildServiceProvider();
            var lockProvider = provider.GetRequiredService<IDistributedLockProvider>();

            Assert.DoesNotContain("Redis", lockProvider.GetType().FullName, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(lockPath))
            {
                Directory.Delete(lockPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DevelopmentWithoutRedisUsesExplicitFileLockOnly()
    {
        var lockPath = Path.Combine(Path.GetTempPath(), "astererp-lock-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["DistributedLock:DevelopmentFilePath"] = lockPath
            });
            var services = new ServiceCollection();
            services.AddAsterErpAbpInfrastructure(configuration);
            await using var provider = services.BuildServiceProvider();
            var lockProvider = provider.GetRequiredService<IDistributedLockProvider>();

            Assert.DoesNotContain("InMemory", lockProvider.GetType().FullName, StringComparison.OrdinalIgnoreCase);
            await using var firstHandle = await lockProvider.CreateLock("registration-test")
                .TryAcquireAsync(TimeSpan.Zero);
            Assert.NotNull(firstHandle);

            await using var secondHandle = await lockProvider.CreateLock("registration-test")
                .TryAcquireAsync(TimeSpan.Zero);
            Assert.Null(secondHandle);
        }
        finally
        {
            if (Directory.Exists(lockPath))
            {
                Directory.Delete(lockPath, recursive: true);
            }
        }
    }

    private static IConfiguration BuildConfiguration(IReadOnlyDictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
