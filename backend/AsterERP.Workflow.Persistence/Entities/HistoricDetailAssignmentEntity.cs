using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_HI_DETAIL")]
public class HistoricDetailAssignmentEntity : HistoricDetailEntity
{
    public const string TypeAssigneeUpdate = "Assignment";

    public HistoricDetailAssignmentEntity()
    {
        Type = TypeAssigneeUpdate;
    }
}
