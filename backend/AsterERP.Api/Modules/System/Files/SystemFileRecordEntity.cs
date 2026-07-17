using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Files;

[SugarTable("system_file_records")]
public sealed class SystemFileRecordEntity : EntityBase
{
    public string FileName { get; set; } = string.Empty;

    public string StoredPath { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public long FileSize { get; set; }

    public string Sha256 { get; set; } = string.Empty;
}
