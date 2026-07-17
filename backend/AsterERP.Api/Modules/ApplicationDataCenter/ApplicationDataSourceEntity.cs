using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_data_sources")]
public sealed class ApplicationDataSourceEntity : ApplicationDataCenterObjectEntity
{
    public bool IsReadOnly { get; set; }

    public bool IsSystemManaged { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? LastValidationFingerprint { get; set; }
}
