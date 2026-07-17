using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_microflows")]
public sealed class ApplicationMicroflowEntity : ApplicationDataCenterObjectEntity
{
    [SugarColumn(IsNullable = true)]
    public string? DefaultEndpointPath { get; set; }

    public int EndpointCount { get; set; }
}
