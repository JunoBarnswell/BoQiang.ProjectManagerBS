using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_GE_BYTEARRAY")]
public class ResourceEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "REV_")]
    public int Revision { get; set; }

    [SugarColumn(ColumnName = "NAME_", IsNullable = true)]
    public string? Name { get; set; }

    [SugarColumn(ColumnName = "DEPLOYMENT_ID_", IsNullable = true)]
    public string? DeploymentId { get; set; }

    [SugarColumn(ColumnName = "BYTES_", IsNullable = true)]
    public byte[]? Bytes { get; set; }

    [SugarColumn(ColumnName = "GENERATED_")]
    public bool Generated { get; set; }
}
