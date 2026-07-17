namespace AsterERP.Api.Infrastructure.Abp.DevelopmentSeed;

public sealed class DevelopmentSeedOptions
{
    public Dictionary<string, string> UserPasswords { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
