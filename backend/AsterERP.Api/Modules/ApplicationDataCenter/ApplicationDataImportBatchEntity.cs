using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_data_import_batches")]
public sealed class ApplicationDataImportBatchEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string ModuleKey { get; set; } = string.Empty;

    public string SourceObjectId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public int TotalRows { get; set; }

    public int SuccessRows { get; set; }

    public int FailedRows { get; set; }

    [SugarColumn(Length = 262144, IsNullable = true)]
    public string? ResultJson { get; set; }
}
