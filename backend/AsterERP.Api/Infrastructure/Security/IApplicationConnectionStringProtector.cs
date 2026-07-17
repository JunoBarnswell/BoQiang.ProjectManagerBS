namespace AsterERP.Api.Infrastructure.Security;

public interface IApplicationConnectionStringProtector
{
    string Protect(string plainText);

    string Unprotect(string cipherText);
}
