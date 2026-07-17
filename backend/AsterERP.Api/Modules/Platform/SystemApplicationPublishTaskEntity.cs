using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Platform;

[SugarTable("system_application_publish_tasks")]
public sealed class SystemApplicationPublishTaskEntity : EntityBase
{
    public string AppId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? Version { get; set; }

    public string Status { get; set; } = "Pending";

    public string Stage { get; set; } = "Queued";

    public int ProgressPercent { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? StartedAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? FinishedAt { get; set; }

    public long DurationMs { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? SourceProjectPath { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ReleasePath { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ArtifactPath { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }

    public string TraceId { get; set; } = string.Empty;

    public string RuntimeIdentifier { get; set; } = "win-x64";

    public bool SelfContained { get; set; } = true;

    public bool IncludeFrontend { get; set; } = true;

    public bool IncludeBackend { get; set; } = true;

    public bool CleanOutput { get; set; }

    public string BackendHost { get; set; } = "127.0.0.1";

    public int BackendPort { get; set; } = 5000;

    public string FrontendBasePath { get; set; } = string.Empty;

    public string FrontendApiBaseUrl { get; set; } = "/api";
}
