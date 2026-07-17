namespace AsterERP.Api.Infrastructure.Security;

public sealed record PasswordVerificationResult(
    bool Success,
    bool NeedsRehash,
    string Status,
    string Format)
{
    public bool RequiresPasswordReset => string.Equals(Status, "PasswordResetRequired", StringComparison.Ordinal);
}
