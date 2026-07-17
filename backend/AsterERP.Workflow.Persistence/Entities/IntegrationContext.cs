using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_RU_INTEGRATION_CONTEXT")]
public class IntegrationContextEntity : AbstractEntity
{
    [SugarColumn(ColumnName = "EXECUTION_ID_", IsNullable = true)]
    public string? ExecutionId { get; set; }

    [SugarColumn(ColumnName = "PROC_INST_ID_", IsNullable = true)]
    public string? ProcessInstanceId { get; set; }

    [SugarColumn(ColumnName = "PROC_DEF_ID_", IsNullable = true)]
    public string? ProcessDefinitionId { get; set; }

    [SugarColumn(ColumnName = "FLOW_NODE_ID_", IsNullable = true)]
    public string? FlowNodeId { get; set; }

    [SugarColumn(ColumnName = "CONNECTOR_ID_", IsNullable = true)]
    public string? ConnectorId { get; set; }

    [SugarColumn(ColumnName = "CORRELATION_ID_", IsNullable = true)]
    public string? CorrelationId { get; set; }

    [SugarColumn(ColumnName = "STATUS_", IsNullable = true)]
    public string? Status { get; set; }

    [SugarColumn(ColumnName = "RESULT_TYPE_", IsNullable = true)]
    public string? ResultType { get; set; }

    [SugarColumn(ColumnName = "CREATED_DATE_", IsNullable = true)]
    public DateTime? CreatedDate { get; set; }

    public override object GetPersistentState()
    {
        return new Dictionary<string, object?>
        {
            ["executionId"] = ExecutionId,
            ["processInstanceId"] = ProcessInstanceId,
            ["processDefinitionId"] = ProcessDefinitionId,
            ["flowNodeId"] = FlowNodeId,
            ["connectorId"] = ConnectorId,
            ["correlationId"] = CorrelationId,
            ["status"] = Status,
            ["resultType"] = ResultType,
            ["createdDate"] = CreatedDate
        };
    }

    public override string ToString()
    {
        return $"IntegrationContext[ executionId='{ExecutionId}', processInstanceId='{ProcessInstanceId}', flowNodeId='{FlowNodeId}' ]";
    }
}
