using System.Security.Cryptography;
using System.Text;
using AsterERP.Api.Modules.System.Users;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Security;

public sealed class PasswordFormatInventoryService
{
    public async Task<PasswordFormatInventoryReport> ScanAsync(
        ISqlSugarClient db,
        string scope,
        CancellationToken cancellationToken = default)
    {
        var normalizedScope = string.IsNullOrWhiteSpace(scope) ? "unspecified" : scope.Trim();
        var hashes = await db.Queryable<SystemUserEntity>()
            .Where(item => !item.IsDeleted)
            .Select(item => item.PasswordHash)
            .ToListAsync(cancellationToken);
        var counts = hashes.Aggregate(
            new FormatCounts(),
            static (current, hash) => current.Add(Classify(hash)));
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var reportHash = CreateReportHash(normalizedScope, generatedAtUtc, counts);
        return new(
            normalizedScope,
            generatedAtUtc,
            hashes.Count,
            counts.VersionedPbkdf2,
            counts.LegacyPbkdf2,
            counts.Sha256,
            counts.SuspectedPlaintext,
            counts.Invalid,
            reportHash);
    }

    private static PasswordFormatKind Classify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return PasswordFormatKind.Invalid;
        if (IsPbkdf2(value, versioned: true))
            return PasswordFormatKind.VersionedPbkdf2;
        if (IsPbkdf2(value, versioned: false))
            return PasswordFormatKind.LegacyPbkdf2;
        if (value.StartsWith("PBKDF2$", StringComparison.Ordinal))
            return PasswordFormatKind.Invalid;
        if (IsSha256(value))
            return PasswordFormatKind.Sha256;
        return value.Contains('$', StringComparison.Ordinal)
            ? PasswordFormatKind.Invalid
            : PasswordFormatKind.SuspectedPlaintext;
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(static character => Uri.IsHexDigit(character));

    private static bool IsPbkdf2(string value, bool versioned)
    {
        var parts = value.Split('$');
        var algorithmIndex = versioned ? 2 : 1;
        var iterationsIndex = versioned ? 3 : 2;
        var saltIndex = versioned ? 4 : 3;
        var hashIndex = versioned ? 5 : 4;
        if (parts.Length != (versioned ? 6 : 5) ||
            !string.Equals(parts[0], "PBKDF2", StringComparison.Ordinal) ||
            (versioned && !string.Equals(parts[1], PasswordHashPolicyOptions.CurrentVersion, StringComparison.Ordinal)) ||
            !string.Equals(parts[algorithmIndex], "HMACSHA256", StringComparison.Ordinal) ||
            !int.TryParse(parts[iterationsIndex], out var iterations) ||
            iterations <= 0 || iterations > 1_000_000)
            return false;

        try
        {
            return Convert.FromBase64String(parts[saltIndex]).Length > 0 && Convert.FromBase64String(parts[hashIndex]).Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string CreateReportHash(string scope, DateTimeOffset generatedAtUtc, FormatCounts counts)
    {
        var payload = string.Join('|',
            scope,
            generatedAtUtc.ToUniversalTime().ToString("O"),
            counts.VersionedPbkdf2,
            counts.LegacyPbkdf2,
            counts.Sha256,
            counts.SuspectedPlaintext,
            counts.Invalid);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private enum PasswordFormatKind
    {
        VersionedPbkdf2,
        LegacyPbkdf2,
        Sha256,
        SuspectedPlaintext,
        Invalid
    }

    private sealed class FormatCounts
    {
        public int VersionedPbkdf2 { get; private set; }
        public int LegacyPbkdf2 { get; private set; }
        public int Sha256 { get; private set; }
        public int SuspectedPlaintext { get; private set; }
        public int Invalid { get; private set; }

        public FormatCounts Add(PasswordFormatKind kind)
        {
            switch (kind)
            {
                case PasswordFormatKind.VersionedPbkdf2: VersionedPbkdf2++; break;
                case PasswordFormatKind.LegacyPbkdf2: LegacyPbkdf2++; break;
                case PasswordFormatKind.Sha256: Sha256++; break;
                case PasswordFormatKind.SuspectedPlaintext: SuspectedPlaintext++; break;
                default: Invalid++; break;
            }

            return this;
        }
    }
}
