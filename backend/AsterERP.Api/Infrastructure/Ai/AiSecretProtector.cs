using Microsoft.AspNetCore.DataProtection;

namespace AsterERP.Api.Infrastructure.Ai;

public sealed class AiSecretProtector(IDataProtectionProvider provider) : IAiSecretProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("AsterERP.Ai.ApiKeys.v1");

    public string Protect(string plainText) => protector.Protect(plainText.Trim());

    public string Unprotect(string cipherText) => protector.Unprotect(cipherText);

    public string Mask(string plainText)
    {
        var value = plainText.Trim();
        if (value.Length <= 8)
        {
            return "****";
        }

        return $"{value[..4]}****{value[^4..]}";
    }
}
