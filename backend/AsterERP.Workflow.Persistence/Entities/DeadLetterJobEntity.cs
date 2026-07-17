using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_RU_DEADLETTER_JOB")]
public class DeadLetterJobEntity : AbstractJobEntityBase
{
    [SugarColumn(ColumnName = "ORIG_JOB_ID_", IsNullable = true)]
    public string? OriginalJobId { get; set; }

    [SugarColumn(ColumnName = "ORIG_JOB_TYPE_", IsNullable = true)]
    public string? OriginalJobType { get; set; }
}
