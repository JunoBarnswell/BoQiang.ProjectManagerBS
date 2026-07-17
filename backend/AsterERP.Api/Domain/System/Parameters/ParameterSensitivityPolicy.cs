namespace AsterERP.Api.Domain.System.Parameters;

public static class ParameterSensitivityPolicy
{
    public const string SensitiveValueMask = "******";

    private static readonly string[] SensitiveKeyFragments =
    [
        "accesskeyid",
        "accesskeysecret",
        "access_key_id",
        "access_key_secret",
        "connectionstring",
        "credential",
        "password",
        "privatekey",
        "private_key",
        "secret",
        "token"
    ];

    public static bool IsSensitiveKey(string paramKey)
    {
        var normalizedKey = paramKey.Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return SensitiveKeyFragments.Any(fragment => normalizedKey.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    public static string MaskValue(string paramKey, string paramValue)
    {
        return IsSensitiveKey(paramKey) && !string.IsNullOrEmpty(paramValue)
            ? SensitiveValueMask
            : paramValue;
    }

    public static bool ShouldKeepExistingValue(string paramKey, string submittedValue, string currentValue)
    {
        return IsSensitiveKey(paramKey) &&
            submittedValue == SensitiveValueMask &&
            !string.IsNullOrEmpty(currentValue);
    }
}
