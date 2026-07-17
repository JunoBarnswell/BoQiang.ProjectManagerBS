namespace AsterERP.Contracts.Logs;

public static class LoginLogResults
{
    public const string Success = "Success";
    public const string AccountNotFound = "AccountNotFound";
    public const string PasswordError = "PasswordError";
    public const string AccountDisabled = "AccountDisabled";

    public static bool IsKnown(string value)
    {
        return string.Equals(value, Success, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, AccountNotFound, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, PasswordError, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, AccountDisabled, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string value)
    {
        if (string.Equals(value, Success, StringComparison.OrdinalIgnoreCase))
        {
            return Success;
        }

        if (string.Equals(value, AccountNotFound, StringComparison.OrdinalIgnoreCase))
        {
            return AccountNotFound;
        }

        if (string.Equals(value, PasswordError, StringComparison.OrdinalIgnoreCase))
        {
            return PasswordError;
        }

        if (string.Equals(value, AccountDisabled, StringComparison.OrdinalIgnoreCase))
        {
            return AccountDisabled;
        }

        return value;
    }
}
