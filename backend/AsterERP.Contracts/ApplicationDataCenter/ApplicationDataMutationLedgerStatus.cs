namespace AsterERP.Contracts.ApplicationDataCenter;

public static class ApplicationDataMutationLedgerStatus
{
    public const string Executing = "Executing";
    public const string Finalized = "Finalized";
    public const string Failed = "Failed";
    public const string Unknown = "Unknown";
    public const string RecoveryRequired = "RecoveryRequired";
}
