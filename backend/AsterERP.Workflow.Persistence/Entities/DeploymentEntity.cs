using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_RE_DEPLOYMENT")]
public class DeploymentEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "NAME_", IsNullable = true)]
    public string? Name { get; set; }

    [SugarColumn(ColumnName = "CATEGORY_", IsNullable = true)]
    public string? Category { get; set; }

    [SugarColumn(ColumnName = "KEY_", IsNullable = true)]
    public string? Key { get; set; }

    [SugarColumn(ColumnName = "TENANT_ID_", IsNullable = true)]
    public string? TenantId { get; set; }

    [SugarColumn(ColumnName = "DEPLOY_TIME_", IsNullable = true)]
    public DateTime? DeployTime { get; set; }

    [SugarColumn(ColumnName = "DERIVED_FROM_", IsNullable = true)]
    public string? DerivedFrom { get; set; }

    [SugarColumn(ColumnName = "ENGINE_VERSION_", IsNullable = true)]
    public string? EngineVersion { get; set; }
}
