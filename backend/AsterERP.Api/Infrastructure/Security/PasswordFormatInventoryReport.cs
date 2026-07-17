namespace AsterERP.Api.Infrastructure.Security;

public sealed record PasswordFormatInventoryReport(
    string Scope,
    DateTimeOffset GeneratedAtUtc,
    int Total,
    int VersionedPbkdf2,
    int LegacyPbkdf2,
    int Sha256,
    int SuspectedPlaintext,
    int Invalid,
    string ReportHash);

