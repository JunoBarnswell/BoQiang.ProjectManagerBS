using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_GE_BYTEARRAY")]
public class ByteArrayEntity : AbstractEntity
{
    [SugarColumn(ColumnName = "NAME_", IsNullable = true)]
    public string? Name { get; set; }

    [SugarColumn(ColumnName = "DEPLOYMENT_ID_", IsNullable = true)]
    public string? DeploymentId { get; set; }

    [SugarColumn(ColumnName = "BYTES_", ColumnDataType = "BLOB", IsNullable = true)]
    public byte[]? Bytes { get; set; }

    [SugarColumn(ColumnName = "GENERATED_")]
    public bool Generated { get; set; }

    public override object GetPersistentState()
    {
        return new { Name, Bytes };
    }

    public override string ToString()
    {
        return $"ByteArrayEntity[id={Id}, name={Name}, size={(Bytes != null ? Bytes.Length : 0)}]";
    }
}
