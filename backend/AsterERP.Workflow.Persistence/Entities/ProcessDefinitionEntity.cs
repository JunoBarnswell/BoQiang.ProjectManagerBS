using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_RE_PROCDEF")]
public class ProcessDefinitionEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "REV_")]
    public int Revision { get; set; }

    [SugarColumn(ColumnName = "CATEGORY_", IsNullable = true)]
    public string? Category { get; set; }

    [SugarColumn(ColumnName = "NAME_", IsNullable = true)]
    public string? Name { get; set; }

    [SugarColumn(ColumnName = "KEY_", IsNullable = true)]
    public string? Key { get; set; }

    [SugarColumn(ColumnName = "VERSION_")]
    public int Version { get; set; }

    [SugarColumn(ColumnName = "DEPLOYMENT_ID_", IsNullable = true)]
    public string? DeploymentId { get; set; }

    [SugarColumn(ColumnName = "RESOURCE_NAME_", IsNullable = true)]
    public string? ResourceName { get; set; }

    [SugarColumn(ColumnName = "DGRM_RESOURCE_NAME_", IsNullable = true)]
    public string? DiagramResourceName { get; set; }

    [SugarColumn(ColumnName = "DESCRIPTION_", IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(ColumnName = "HAS_START_FORM_KEY_")]
    public bool HasStartFormKey { get; set; }

    [SugarColumn(ColumnName = "HAS_GRAPHICAL_NOTATION_")]
    public bool HasGraphicalNotation { get; set; } = true;

    [SugarColumn(ColumnName = "SUSPENSION_STATE_")]
    public int SuspensionState { get; set; } = 1;

    [SugarColumn(ColumnName = "TENANT_ID_", IsNullable = true)]
    public string? TenantId { get; set; }

    [SugarColumn(ColumnName = "DERIVED_FROM_", IsNullable = true)]
    public string? DerivedFrom { get; set; }

    [SugarColumn(ColumnName = "DERIVED_FROM_ROOT_", IsNullable = true)]
    public string? DerivedFromRoot { get; set; }

    [SugarColumn(ColumnName = "DERIVED_VERSION_")]
    public int DerivedVersion { get; set; }

    [SugarColumn(ColumnName = "ENGINE_VERSION_", IsNullable = true)]
    public string? EngineVersion { get; set; }
}
