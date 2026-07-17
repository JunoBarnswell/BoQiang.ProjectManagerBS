namespace AsterERP.Api.Infrastructure.Ai;

public interface IAiSecretProtector
{
    string Protect(string plainText);

    string Unprotect(string cipherText);

    string Mask(string plainText);
}
