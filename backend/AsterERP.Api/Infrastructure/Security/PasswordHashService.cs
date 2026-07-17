using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AsterERP.Api.Infrastructure.Security;

public sealed class PasswordHashService
    : IPasswordHashService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 210_000;
    private const int MaximumAcceptedIterations = 1_000_000;
    private const string FormatPrefix = "PBKDF2";
    private const string Algorithm = "HMACSHA256";

    private readonly PasswordHashPolicyOptions policy;
    private readonly TimeProvider timeProvider;

    public PasswordHashService(PasswordHashPolicyOptions? policy = null, TimeProvider? timeProvider = null)
    {
        this.policy = policy ?? new PasswordHashPolicyOptions();
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return string.Join('$',
            FormatPrefix,
            PasswordHashPolicyOptions.CurrentVersion,
            Algorithm,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public PasswordVerificationResult Verify(string storedPassword, string inputPassword)
    {
        if (string.IsNullOrWhiteSpace(storedPassword))
            return PasswordResetRequired("empty");

        if (storedPassword.StartsWith($"{FormatPrefix}${PasswordHashPolicyOptions.CurrentVersion}$", StringComparison.Ordinal))
            return VerifyPbkdf2(storedPassword, inputPassword, versioned: true);

        if (storedPassword.StartsWith($"{FormatPrefix}$v", StringComparison.Ordinal))
            return PasswordResetRequired("invalid-legacy-pbkdf2");

        if (storedPassword.StartsWith($"{FormatPrefix}$", StringComparison.Ordinal))
        {
            if (!policy.IsLegacyAcceptanceOpen(timeProvider.GetUtcNow()))
                return PasswordResetRequired("legacy-pbkdf2");
            return VerifyPbkdf2(storedPassword, inputPassword, versioned: false);
        }

        if (IsSha256(storedPassword) || !storedPassword.Contains('$', StringComparison.Ordinal))
            return PasswordResetRequired(IsSha256(storedPassword) ? "sha256" : "suspected-plaintext");

        return PasswordResetRequired("invalid");
    }

    private static PasswordVerificationResult VerifyPbkdf2(string storedPassword, string inputPassword, bool versioned)
    {
        var parts = storedPassword.Split('$');
        var algorithmIndex = versioned ? 2 : 1;
        var iterationsIndex = versioned ? 3 : 2;
        var saltIndex = versioned ? 4 : 3;
        var hashIndex = versioned ? 5 : 4;
        if (parts.Length != (versioned ? 6 : 5) ||
            !string.Equals(parts[0], FormatPrefix, StringComparison.Ordinal) ||
            (versioned && !string.Equals(parts[1], PasswordHashPolicyOptions.CurrentVersion, StringComparison.Ordinal)) ||
            !string.Equals(parts[algorithmIndex], Algorithm, StringComparison.Ordinal) ||
            !int.TryParse(parts[iterationsIndex], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) ||
            iterations <= 0 || iterations > MaximumAcceptedIterations)
            return PasswordResetRequired(versioned ? "invalid-pbkdf2-v1" : "invalid-legacy-pbkdf2");

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[saltIndex]);
            expectedHash = Convert.FromBase64String(parts[hashIndex]);
        }
        catch (FormatException)
        {
            return PasswordResetRequired(versioned ? "invalid-pbkdf2-v1" : "invalid-legacy-pbkdf2");
        }

        if (salt.Length == 0 || expectedHash.Length == 0)
            return PasswordResetRequired(versioned ? "invalid-pbkdf2-v1" : "invalid-legacy-pbkdf2");

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(inputPassword, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
        var success = CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        return success
            ? Result(true, versioned && iterations >= Iterations ? "Success" : "LegacyAccepted", versioned ? "pbkdf2-v1" : "legacy-pbkdf2", !versioned || iterations < Iterations)
            : Result(false, "InvalidCredentials", versioned ? "pbkdf2-v1" : "legacy-pbkdf2");
    }

    private static PasswordVerificationResult Result(bool success, string status, string format, bool needsRehash = false) =>
        new(success, needsRehash, status, format);

    private static PasswordVerificationResult PasswordResetRequired(string format) =>
        Result(false, "PasswordResetRequired", format);

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(static character => Uri.IsHexDigit(character));
}
