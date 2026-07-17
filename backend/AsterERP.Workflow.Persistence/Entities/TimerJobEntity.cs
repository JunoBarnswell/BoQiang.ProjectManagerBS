using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_RU_TIMER_JOB")]
public class TimerJobEntity : AbstractJobEntityBase
{
    [SugarColumn(ColumnName = "LOCK_EXP_TIME_", IsNullable = true)]
    public DateTime? LockExpirationTime { get; set; }

    [SugarColumn(ColumnName = "LOCK_OWNER_", IsNullable = true)]
    public string? LockOwner { get; set; }

    public override Dictionary<string, object?> GetPersistentState()
    {
        var persistentState = base.GetPersistentState();
        persistentState["lockOwner"] = LockOwner;
        persistentState["lockExpirationTime"] = LockExpirationTime;
        return persistentState;
    }
}
