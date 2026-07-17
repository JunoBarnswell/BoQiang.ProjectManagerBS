using AsterERP.Contracts.ApplicationDataCenter;

namespace AsterERP.Api.Application.ApplicationDataCenter.Providers;

public sealed partial record ApplicationDataSourceProviderCapability(
    string Provider,
    bool SupportsTransactionalDdl,
    bool SupportsAtomicViewReplace,
    bool SupportsRowVersion,
    bool SupportsReturning,
    bool SupportsJson,
    bool SupportsCancellation,
    int MaxPageSize);

public sealed partial record ApplicationDataSourceProviderCapability
{
    public ApplicationDataSourceConnectionCapability Connection => ApplicationDataSourceConnectionCapability.ForProvider(Provider);

    public ApplicationDataSourceSupportLevel SupportLevel =>
        SupportsStructuredMappingCache && SupportsMappingCacheParameters
            ? ApplicationDataSourceSupportLevel.Full
            : SupportsStructuredMappingCache
                ? ApplicationDataSourceSupportLevel.ReadOnly
                : ApplicationDataSourceSupportLevel.MetadataOnly;

    public string? SupportReason => SupportLevel switch
    {
        ApplicationDataSourceSupportLevel.Full => null,
        ApplicationDataSourceSupportLevel.ReadOnly => "The provider supports mapping-cache reads but not parameterized mapping-cache execution.",
        ApplicationDataSourceSupportLevel.MetadataOnly => "The provider exposes metadata but does not support structured mapping-cache execution.",
        _ => "The provider is unsupported."
    };

    public IReadOnlyList<ApplicationDataSourceFeatureSupport> FeatureSupport =>
    [
        new("structuredMappingCache", SupportLevel, SupportReason),
        new("mappingCacheParameters", SupportsMappingCacheParameters
            ? ApplicationDataSourceSupportLevel.Full
            : ApplicationDataSourceSupportLevel.Unsupported,
            SupportsMappingCacheParameters ? null : "The provider does not support mapping-cache parameters."),
        new("schemas", SupportsSchemas
            ? ApplicationDataSourceSupportLevel.Full
            : ApplicationDataSourceSupportLevel.Unsupported,
            SupportsSchemas ? null : "The provider does not support schema-qualified objects.")
    ];

    public IReadOnlyList<ApplicationQueryJoinType> SupportedJoinTypes => Provider switch
    {
        "PostgreSQL" or "SqlServer" => [ApplicationQueryJoinType.Inner, ApplicationQueryJoinType.Left, ApplicationQueryJoinType.Right, ApplicationQueryJoinType.Full],
        "MySql" or "Sqlite" => [ApplicationQueryJoinType.Inner, ApplicationQueryJoinType.Left, ApplicationQueryJoinType.Right],
        _ => []
    };

    public int MaxWriteRows { get; init; } = 1000;

    public int MaxPreviewRows { get; init; } = 200;

    public bool SupportsSchemas { get; init; }

    public bool SupportsOriginalValueConcurrency { get; init; } = true;

    public bool SupportsStructuredMappingCache { get; init; } = true;

    public bool SupportsMappingCacheParameters { get; init; } = true;

    public int MaxMappingCacheColumns { get; init; } = 100;

    public int MaxMappingCacheParameters { get; init; } = 50;
}
