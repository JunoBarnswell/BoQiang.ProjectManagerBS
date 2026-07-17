using Microsoft.Extensions.Options;

namespace AsterERP.Api.Infrastructure.Abp.DevelopmentSeed;

public sealed class DevelopmentSeedOptionsValidator : IValidateOptions<DevelopmentSeedOptions>
{
    public ValidateOptionsResult Validate(string? name, DevelopmentSeedOptions options)
    {
        var invalidEntries = options.UserPasswords
            .Where(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value))
            .Select(item => item.Key)
            .ToArray();

        return invalidEntries.Length == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"DevelopmentSeed:UserPasswords contains invalid entries: {string.Join(", ", invalidEntries)}");
    }
}
