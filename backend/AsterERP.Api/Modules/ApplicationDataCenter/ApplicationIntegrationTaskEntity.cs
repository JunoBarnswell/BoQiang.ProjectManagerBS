using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_integration_tasks")]
public sealed class ApplicationIntegrationTaskEntity : ApplicationDataCenterObjectEntity
{
    [SugarColumn(IsNullable = true)]
    public string? SourceObjectId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? TargetObjectId { get; set; }

    public string TriggerMode { get; set; } = "Manual";

    public bool IsEnabled { get; set; } = true;
}
