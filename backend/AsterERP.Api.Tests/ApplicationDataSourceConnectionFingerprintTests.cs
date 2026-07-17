using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Modules.ApplicationDataCenter;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataSourceConnectionFingerprintTests
{
    [Fact]
    public void Fingerprint_is_deterministic_and_excludes_secret_plaintext()
    {
        var first = new ApplicationDataSourceEntity
        {
            ObjectType = "PostgreSql",
            ConfigJson = "{\"port\":5432,\"host\":\"db.internal\"}",
            SecretRef = "source-1:secret",
            SecretConfigCipherText = "cipher-a"
        };
        var reordered = new ApplicationDataSourceEntity
        {
            ObjectType = first.ObjectType,
            ConfigJson = "{\"host\":\"db.internal\",\"port\":5432}",
            SecretRef = first.SecretRef,
            SecretConfigCipherText = first.SecretConfigCipherText
        };

        var fingerprint = ApplicationDataSourceConnectionFingerprint.Compute(first);

        Assert.Equal(fingerprint, ApplicationDataSourceConnectionFingerprint.Compute(reordered));
        Assert.DoesNotContain("cipher-a", fingerprint, StringComparison.Ordinal);
        Assert.NotEqual(fingerprint, ApplicationDataSourceConnectionFingerprint.Compute(new ApplicationDataSourceEntity
        {
            ObjectType = first.ObjectType,
            ConfigJson = first.ConfigJson,
            SecretRef = first.SecretRef,
            SecretConfigCipherText = "cipher-b"
        }));
    }
}
