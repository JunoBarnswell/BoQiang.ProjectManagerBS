namespace AsterERP.Workflow.Approval.Api.Models.Workflow;

[SqlSugar.SugarTable("act_re_procdef")]
public class WorkflowProcessDefinition
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true, ColumnName = "id_")]
    public string? Id { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "name_")]
    public string? Name { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "category_")]
    public string? Category { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "deployment_id_")]
    public string? DeploymentId { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "key_")]
    public string? Key { get; set; }
}
