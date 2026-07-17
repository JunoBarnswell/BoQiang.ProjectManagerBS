namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed class CacheInfrastructureSettingsUpdate
{
    public string? Provider { get; set; }

    public SecretSettingUpdate? RedisConfiguration { get; set; }

    public int? DefaultExpirationMinutes { get; set; }
}
