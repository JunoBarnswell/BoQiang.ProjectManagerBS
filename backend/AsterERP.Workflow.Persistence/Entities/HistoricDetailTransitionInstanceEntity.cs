using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_HI_DETAIL")]
public class HistoricDetailTransitionInstanceEntity : HistoricDetailEntity
{
    public const string TypeTransition = "transition";

    public HistoricDetailTransitionInstanceEntity()
    {
        Type = TypeTransition;
    }

    [SugarColumn(ColumnName = "ACTIVITY_ID_", IsNullable = true)]
    public string? ActivityId { get; set; }

    [SugarColumn(ColumnName = "OLD_", IsNullable = true)]
    public string? OldActivityId { get; set; }

    [SugarColumn(ColumnName = "ACTIVITY_TYPE_", IsNullable = true)]
    public string? ActivityType { get; set; }

    [SugarColumn(ColumnName = "TRANSITION_ID_", IsNullable = true)]
    public string? TransitionId { get; set; }
}
