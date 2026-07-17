using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.DataProtection;

namespace AsterERP.Api.Infrastructure.Security;

public sealed class ApplicationConnectionStringProtector(IDataProtectionProvider provider) : IApplicationConnectionStringProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("AsterERP.ApplicationDatabase.ConnectionString.v1");

    public string Protect(string plainText) => protector.Protect(plainText.Trim());

    public string Unprotect(string cipherText)
    {
        try
        {
            return protector.Unprotect(cipherText.Trim());
        }
        catch (Exception ex) when (ex is not ValidationException)
        {
            throw new ValidationException("应用数据库绑定配置已失效，请重新绑定");
        }
    }
}
