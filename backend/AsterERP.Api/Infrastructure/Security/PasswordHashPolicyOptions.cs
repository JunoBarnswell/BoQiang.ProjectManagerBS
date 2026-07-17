namespace AsterERP.Api.Infrastructure.Security;

public sealed class PasswordHashPolicyOptions
{
    public const string SectionName = "Security:PasswordHash";
    public const string CurrentVersion = "v1";

    public DateTimeOffset? LegacyAcceptanceUntilUtc { get; set; }

    public bool IsLegacyAcceptanceOpen(DateTimeOffset now) =>
        LegacyAcceptanceUntilUtc is { } cutoff && now < cutoff;
}

