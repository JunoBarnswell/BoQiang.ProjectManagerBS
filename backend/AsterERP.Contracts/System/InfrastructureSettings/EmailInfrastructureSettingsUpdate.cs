namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed class EmailInfrastructureSettingsUpdate
{
    public bool? Enabled { get; set; }

    public string? SmtpHost { get; set; }

    public int? SmtpPort { get; set; }

    public string? UserName { get; set; }

    public SecretSettingUpdate? Password { get; set; }

    public string? DefaultFromAddress { get; set; }

    public string? DefaultFromDisplayName { get; set; }

    public bool? EnableSsl { get; set; }
}
