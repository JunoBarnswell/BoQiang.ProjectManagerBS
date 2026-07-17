namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed class SecretSettingUpdate
{
    public string? Value { get; set; }

    public bool Clear { get; set; }
}
