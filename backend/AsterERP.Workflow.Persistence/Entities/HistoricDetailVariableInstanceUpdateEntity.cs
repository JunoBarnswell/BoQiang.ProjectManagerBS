using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_HI_DETAIL")]
public class HistoricDetailVariableInstanceUpdateEntity : HistoricDetailEntity
{
    public const string TypeVariableUpdate = "VariableUpdate";

    public HistoricDetailVariableInstanceUpdateEntity()
    {
        Type = TypeVariableUpdate;
    }

    public ByteArrayRef? ByteArrayRef { get; set; }

    public string? VariableTypeName { get; set; }
}
