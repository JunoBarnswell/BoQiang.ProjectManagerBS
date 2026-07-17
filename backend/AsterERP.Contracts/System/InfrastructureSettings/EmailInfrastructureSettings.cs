namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed record EmailInfrastructureSettings(
    bool Enabled,
    string? SmtpHost,
    int? SmtpPort,
    string? UserName,
    SecretSettingState Password,
    string? DefaultFromAddress,
    string? DefaultFromDisplayName,
    bool EnableSsl);
