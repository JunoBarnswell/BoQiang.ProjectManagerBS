namespace AsterERP.Api.Infrastructure.Security;

public interface IPasswordHashService
{
    string HashPassword(string password);

    PasswordVerificationResult Verify(string storedPassword, string inputPassword);
}
