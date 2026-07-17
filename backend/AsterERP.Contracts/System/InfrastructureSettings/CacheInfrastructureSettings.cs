namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed record CacheInfrastructureSettings(
    string Provider,
    SecretSettingState RedisConfiguration,
    int DefaultExpirationMinutes);
