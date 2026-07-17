using System.Security.Cryptography;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.System.Users;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class PasswordHashServiceTests
{
    [Fact]
    public void HashPassword_ProducesVersionedPbkdf2AndVerifiesLatestPassword()
    {
        var service = new PasswordHashService();
        var hash = service.HashPassword("correct-password");

        Assert.StartsWith("PBKDF2$v1$HMACSHA256$", hash, StringComparison.Ordinal);
        Assert.True(service.Verify(hash, "correct-password").Success);
        Assert.False(service.Verify(hash, "wrong-password").Success);
    }

    [Fact]
    public void LegacyPbkdf2_IsAcceptedOnlyInsideConfiguredWindowAndRehashed()
    {
        var legacy = CreateLegacyPbkdf2("legacy-password");
        var openService = new PasswordHashService(new PasswordHashPolicyOptions
        {
            LegacyAcceptanceUntilUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        });
        var closedService = new PasswordHashService();

        var accepted = openService.Verify(legacy, "legacy-password");
        var closed = closedService.Verify(legacy, "legacy-password");

        Assert.True(accepted.Success);
        Assert.True(accepted.NeedsRehash);
        Assert.Equal("legacy-pbkdf2", accepted.Format);
        Assert.False(closed.Success);
        Assert.True(closed.RequiresPasswordReset);
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "sha256")]
    [InlineData("plain-text-legacy-password", "suspected-plaintext")]
    public void NonPbkdf2LegacyFormats_RequireResetWithoutTryingThePassword(string stored, string expectedFormat)
    {
        var result = new PasswordHashService().Verify(stored, "plain-text-legacy-password");

        Assert.False(result.Success);
        Assert.True(result.RequiresPasswordReset);
        Assert.Equal(expectedFormat, result.Format);
    }

    [Theory]
    [InlineData("PBKDF2$v1$not-a-valid-format", "invalid-pbkdf2-v1")]
    [InlineData("PBKDF2$v2$HMACSHA256$210000$bad$bad", "invalid-legacy-pbkdf2")]
    [InlineData("", "empty")]
    public void MalformedStoredFormat_RequiresPasswordReset(string stored, string expectedFormat)
    {
        var result = new PasswordHashService().Verify(stored, "anything");

        Assert.False(result.Success);
        Assert.True(result.RequiresPasswordReset);
        Assert.Equal(expectedFormat, result.Format);
    }

    [Fact]
    public async Task Inventory_ReturnsSanitizedFormatCountsAndDoesNotExposeHashes()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:password-inventory-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = false
        });
        db.CodeFirst.InitTables<SystemUserEntity>();
        db.Insertable(new[]
        {
            new SystemUserEntity { UserName = "v1", PasswordHash = new PasswordHashService().HashPassword("a") },
            new SystemUserEntity { UserName = "legacy", PasswordHash = CreateLegacyPbkdf2("b") },
            new SystemUserEntity { UserName = "sha", PasswordHash = new string('A', 64) },
            new SystemUserEntity { UserName = "plain", PasswordHash = "legacy-password" },
            new SystemUserEntity { UserName = "invalid", PasswordHash = "PBKDF2$v1$invalid" }
        }).ExecuteCommand();

        var report = await new PasswordFormatInventoryService().ScanAsync(db, "test-snapshot");

        Assert.Equal(5, report.Total);
        Assert.Equal(1, report.VersionedPbkdf2);
        Assert.Equal(1, report.LegacyPbkdf2);
        Assert.Equal(1, report.Sha256);
        Assert.Equal(1, report.SuspectedPlaintext);
        Assert.Equal(1, report.Invalid);
        Assert.DoesNotContain("legacy-password", report.ReportHash, StringComparison.Ordinal);
        Assert.NotEmpty(report.ReportHash);
    }

    private static string CreateLegacyPbkdf2(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA256, 32);
        return string.Join('$', "PBKDF2", "HMACSHA256", "210000", Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }
}
