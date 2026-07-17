using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

public abstract class AbstractJobEntityBase : AbstractEntity
{
    public const int DefaultRetries = 3;
    public const int MaxExceptionMessageLength = 2000;
    public const bool DefaultExclusive = true;

    [SugarColumn(ColumnName = "TYPE_", IsNullable = true)]
    public string? JobType { get; set; }

    [SugarColumn(ColumnName = "DUEDATE_", IsNullable = true)]
    public DateTime? DueDate { get; set; }

    [SugarColumn(ColumnName = "RETRIES_")]
    public int Retries { get; set; } = DefaultRetries;

    [SugarColumn(ColumnName = "EXCLUSIVE_")]
    public bool IsExclusive { get; set; } = DefaultExclusive;

    [SugarColumn(ColumnName = "EXECUTION_ID_", IsNullable = true)]
    public string? ExecutionId { get; set; }

    [SugarColumn(ColumnName = "PROCESS_INSTANCE_ID_", IsNullable = true)]
    public string? ProcessInstanceId { get; set; }

    [SugarColumn(ColumnName = "PROC_DEF_ID_", IsNullable = true)]
    public string? ProcessDefinitionId { get; set; }

    [SugarColumn(ColumnName = "REPEAT_", IsNullable = true)]
    public string? Repeat { get; set; }

    [SugarColumn(ColumnName = "HANDLER_TYPE_", IsNullable = true)]
    public string? HandlerType { get; set; }

    [SugarColumn(ColumnName = "HANDLER_CFG_", IsNullable = true)]
    public string? HandlerConfiguration { get; set; }

    [SugarColumn(ColumnName = "EXCEPTION_STACK_ID_", IsNullable = true)]
    public string? ExceptionStackId { get; set; }

    [SugarColumn(ColumnName = "EXCEPTION_MSG_", IsNullable = true)]
    public string? ExceptionMessage { get; set; }

    [SugarColumn(ColumnName = "TENANT_ID_", IsNullable = true)]
    public string? TenantId { get; set; } = string.Empty;

    [SugarColumn(IsIgnore = true)]
    public ByteArrayRef? ExceptionByteArrayRef { get; set; }

    public int MaxIterations { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? EndDate { get; set; }

    public void SetExecution(ExecutionEntity execution)
    {
        ExecutionId = execution.Id;
        ProcessInstanceId = execution.ProcessInstanceId;
        ProcessDefinitionId = execution.ProcessDefinitionId;
    }

    public string? GetExceptionStacktrace()
    {
        if (ExceptionByteArrayRef == null) return null;
        var bytes = ExceptionByteArrayRef.GetBytes();
        if (bytes == null) return null;
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    public void SetExceptionStacktrace(string? exception)
    {
        if (exception == null) return;
        ExceptionByteArrayRef ??= new ByteArrayRef();
        ExceptionByteArrayRef.SetValue("stacktrace", System.Text.Encoding.UTF8.GetBytes(exception));
    }

    public override Dictionary<string, object?> GetPersistentState()
    {
        var persistentState = new Dictionary<string, object?>
        {
            ["retries"] = Retries,
            ["duedate"] = DueDate,
            ["exceptionMessage"] = ExceptionMessage
        };

        if (ExceptionByteArrayRef != null)
        {
            persistentState["exceptionByteArrayId"] = ExceptionByteArrayRef.Id;
        }

        return persistentState;
    }

    public override string ToString()
    {
        return $"{GetType().Name} [id={Id}]";
    }
}
