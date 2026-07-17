using AsterERP.Api.Infrastructure.Abp.ObjectStorage;
using AsterERP.Api.Infrastructure.Abp.Settings;
using Medallion.Threading;
using Medallion.Threading.FileSystem;
using Medallion.Threading.Redis;
using StackExchange.Redis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.FileSystem;
using Volo.Abp.BlobStoring.Minio;

namespace AsterERP.Api.Infrastructure.Abp;

public static class AsterErpAbpServiceCollectionExtensions
{
    public static IServiceCollection AddAsterErpAbpInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        RegisterDistributedLockProvider(services, configuration);
        RegisterDistributedCache(services, configuration);

        services.Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.Configure<AsterErpFileBlobContainer>(container =>
            {
                var provider = configuration[AsterErpSettingNames.ObjectStorageProvider] ??
                    configuration["ObjectStorage:Provider"] ??
                    "FileSystem";

                if (provider.Equals("Minio", StringComparison.OrdinalIgnoreCase))
                {
                    container.UseMinio(minio =>
                    {
                        minio.EndPoint = ResolveRequired(configuration, AsterErpSettingNames.ObjectStorageMinioEndpoint, "ObjectStorage:Minio:Endpoint");
                        minio.BucketName = ResolveRequired(configuration, AsterErpSettingNames.ObjectStorageMinioBucketName, "ObjectStorage:Minio:BucketName");
                        minio.AccessKey = ResolveRequired(configuration, AsterErpSettingNames.ObjectStorageMinioAccessKey, "ObjectStorage:Minio:AccessKey");
                        minio.SecretKey = ResolveRequired(configuration, AsterErpSettingNames.ObjectStorageMinioSecretKey, "ObjectStorage:Minio:SecretKey");
                        minio.WithSSL = ResolveBool(
                            configuration,
                            AsterErpSettingNames.ObjectStorageMinioWithSsl,
                            "ObjectStorage:Minio:WithSSL",
                            defaultValue: false);
                    });
                    return;
                }

                container.UseFileSystem(fileSystem =>
                {
                    fileSystem.BasePath = ResolveFileSystemBasePath(configuration);
                    fileSystem.AppendContainerNameToBasePath = ResolveAppendContainerName(configuration);
                });
            });
        });

        return services;
    }

    private static void RegisterDistributedCache(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConfiguration = configuration[AsterErpSettingNames.CacheRedisConfiguration] ??
            configuration["Cache:Redis:Configuration"] ??
            configuration["Redis:Configuration"];
        if (!string.IsNullOrWhiteSpace(redisConfiguration))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConfiguration.Trim();
                options.InstanceName = configuration["Cache:Redis:InstanceName"] ?? "astererp:";
            });
            return;
        }

        services.AddDistributedMemoryCache();
    }

    private static void RegisterDistributedLockProvider(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"] ??
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
            Environments.Production;
        var redisConfiguration = configuration[AsterErpSettingNames.CacheRedisConfiguration] ??
            configuration["Cache:Redis:Configuration"] ??
            configuration["Redis:Configuration"];

        if (!string.IsNullOrWhiteSpace(redisConfiguration))
        {
            var connection = ConnectionMultiplexer.Connect(redisConfiguration.Trim());
            services.AddSingleton<IConnectionMultiplexer>(connection);
            services.AddSingleton<IDistributedLockProvider>(_ =>
                new RedisDistributedSynchronizationProvider(connection.GetDatabase()));
            return;
        }

        if (!environmentName.Equals(Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "生产环境必须配置 AsterERP.Cache.Redis.Configuration（或 Cache:Redis:Configuration），禁止使用单机锁。");
        }

        var lockPath = configuration["DistributedLock:DevelopmentFilePath"] ??
            Path.Combine(AppContext.BaseDirectory, "data", "development-locks");
        var fullLockPath = Path.GetFullPath(lockPath);
        Directory.CreateDirectory(fullLockPath);
        services.AddSingleton<IDistributedLockProvider>(_ =>
            new FileDistributedSynchronizationProvider(new DirectoryInfo(fullLockPath)));
    }

    private static string ResolveFileSystemBasePath(IConfiguration configuration)
    {
        var configuredPath =
            configuration[AsterErpSettingNames.ObjectStorageFileSystemBasePath] ??
            configuration["ObjectStorage:FileSystem:BasePath"] ??
            "./data/uploads";

        return Path.GetFullPath(configuredPath);
    }

    private static bool ResolveAppendContainerName(IConfiguration configuration) =>
        ResolveBool(
            configuration,
            AsterErpSettingNames.ObjectStorageFileSystemAppendContainerName,
            "ObjectStorage:FileSystem:AppendContainerNameToBasePath",
            defaultValue: false);

    private static string ResolveRequired(IConfiguration configuration, string settingKey, string configurationKey)
    {
        var value = configuration[settingKey] ?? configuration[configurationKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{configurationKey} 未配置，无法启用 MinIO 对象存储");
        }

        return value.Trim();
    }

    private static bool ResolveBool(
        IConfiguration configuration,
        string settingKey,
        string configurationKey,
        bool defaultValue)
    {
        var rawValue = configuration[settingKey] ?? configuration[configurationKey];
        return bool.TryParse(rawValue, out var parsed) ? parsed : defaultValue;
    }
}
