using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_RE_MODEL")]
public class ModelEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "REV_")]
    public int Revision { get; set; }

    [SugarColumn(ColumnName = "NAME_", IsNullable = true)]
    public string? Name { get; set; }

    [SugarColumn(ColumnName = "KEY_", IsNullable = true)]
    public string? Key { get; set; }

    [SugarColumn(ColumnName = "CATEGORY_", IsNullable = true)]
    public string? Category { get; set; }

    [SugarColumn(ColumnName = "CREATE_TIME_", IsNullable = true)]
    public DateTime? CreateTime { get; set; }

    [SugarColumn(ColumnName = "LAST_UPDATE_TIME_", IsNullable = true)]
    public DateTime? LastUpdateTime { get; set; }

    [SugarColumn(ColumnName = "VERSION_")]
    public int Version { get; set; } = 1;

    [SugarColumn(ColumnName = "META_INFO_", IsNullable = true)]
    public string? MetaInfo { get; set; }

    [SugarColumn(ColumnName = "DEPLOYMENT_ID_", IsNullable = true)]
    public string? DeploymentId { get; set; }

    [SugarColumn(ColumnName = "EDITOR_SOURCE_VALUE_ID_", IsNullable = true)]
    public string? EditorSourceValueId { get; set; }

    [SugarColumn(ColumnName = "EDITOR_SOURCE_EXTRA_VALUE_ID_", IsNullable = true)]
    public string? EditorSourceExtraValueId { get; set; }

    [SugarColumn(ColumnName = "TENANT_ID_", IsNullable = true)]
    public string? TenantId { get; set; } = string.Empty;
}
