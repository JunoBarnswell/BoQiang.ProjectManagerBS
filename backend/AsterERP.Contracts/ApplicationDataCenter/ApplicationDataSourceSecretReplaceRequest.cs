namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationDataSourceSecretReplaceRequest
{
    public string SecretConfigJson { get; set; } = "{}";

    public string Reason { get; set; } = string.Empty;
}
