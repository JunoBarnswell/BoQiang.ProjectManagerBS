using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_RU_JOB")]
public class JobEntity : AbstractJobEntityBase
{
    [SugarColumn(ColumnName = "LOCK_EXP_TIME_", IsNullable = true)]
    public DateTime? LockExpirationTime { get; set; }

    [SugarColumn(ColumnName = "LOCK_OWNER_", IsNullable = true)]
    public string? LockOwner { get; set; }

    [SugarColumn(ColumnName = "STATE_")]
    public int State { get; set; }

    [SugarColumn(ColumnName = "CREATED_TIME_", IsNullable = true)]
    public DateTime? CreatedTime { get; set; }

    public override Dictionary<string, object?> GetPersistentState()
    {
        var persistentState = base.GetPersistentState();
        persistentState["lockOwner"] = LockOwner;
        persistentState["lockExpirationTime"] = LockExpirationTime;
        return persistentState;
    }
}
