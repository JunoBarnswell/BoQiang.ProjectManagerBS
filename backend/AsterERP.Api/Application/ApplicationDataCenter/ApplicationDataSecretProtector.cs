using System.Text.Json;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.DataProtection;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataSecretProtector(IDataProtectionProvider provider) : IApplicationDataSecretProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("AsterERP.ApplicationDataCenter.SecretConfig.v1");

    public string Protect(string plainText) => protector.Protect(plainText.Trim());

    public string Unprotect(string cipherText)
    {
        try
        {
            return protector.Unprotect(cipherText.Trim());
        }
        catch (Exception ex) when (ex is not ValidationException)
        {
            throw new ValidationException("数据中心凭据配置已失效，请重新保存");
        }
    }

    public string BuildPublicSecretSummary(string? cipherText)
        => BuildPublicSecretSummary(cipherText, string.Empty, null);

    public string BuildPublicSecretSummary(string? cipherText, string secretRef, DateTime? updatedAt)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return "{}";
        }

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["hasSecret"] = true,
            ["masked"] = true,
            ["secretRef"] = secretRef,
            ["updatedAt"] = updatedAt
        });
    }
}
