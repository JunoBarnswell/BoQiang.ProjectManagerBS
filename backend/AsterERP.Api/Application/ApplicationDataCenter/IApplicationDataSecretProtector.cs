namespace AsterERP.Api.Application.ApplicationDataCenter;

public interface IApplicationDataSecretProtector
{
    string Protect(string plainText);

    string Unprotect(string cipherText);

    string BuildPublicSecretSummary(string? cipherText);

    string BuildPublicSecretSummary(string? cipherText, string secretRef, DateTime? updatedAt);
}
