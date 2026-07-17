using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Platform;

[SugarTable("system_application_publish_profiles")]
public sealed class SystemApplicationPublishProfileEntity : EntityBase
{
    public string AppCode { get; set; } = string.Empty;

    public string TenantScope { get; set; } = "All";

    public string RuntimeIdentifier { get; set; } = "win-x64";

    public bool SelfContained { get; set; } = true;

    [SugarColumn(IsNullable = true)]
    public string? OutputRoot { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? FrontendBasePath { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? BackendHost { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? BackendPort { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? FrontendApiBaseUrl { get; set; }

    public int KeepSuccessfulCount { get; set; } = 5;

    public bool IncludeFrontend { get; set; } = true;

    public bool IncludeBackend { get; set; } = true;
}
