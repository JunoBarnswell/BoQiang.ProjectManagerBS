namespace AsterERP.Contracts.ApplicationDataCenter;

/// <summary>Describes the strongest production capability available to a supported data source.</summary>
public enum ApplicationDataSourceSupportLevel
{
    Unsupported = 0,
    MetadataOnly = 1,
    ReadOnly = 2,
    Full = 3
}

public sealed record ApplicationDataSourceFeatureSupport(
    string Feature,
    ApplicationDataSourceSupportLevel SupportLevel,
    string? Reason = null);
