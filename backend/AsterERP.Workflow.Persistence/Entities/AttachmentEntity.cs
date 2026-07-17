using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_HI_ATTACHMENT")]
public class AttachmentEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "REV_")]
    public int Revision { get; set; }

    [SugarColumn(ColumnName = "USER_ID_", IsNullable = true)]
    public string? UserId { get; set; }

    [SugarColumn(ColumnName = "NAME_", IsNullable = true)]
    public string? Name { get; set; }

    [SugarColumn(ColumnName = "DESCRIPTION_", IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(ColumnName = "TYPE_", IsNullable = true)]
    public string? Type { get; set; }

    [SugarColumn(ColumnName = "TASK_ID_", IsNullable = true)]
    public string? TaskId { get; set; }

    [SugarColumn(ColumnName = "PROC_INST_ID_", IsNullable = true)]
    public string? ProcessInstanceId { get; set; }

    [SugarColumn(ColumnName = "URL_", IsNullable = true)]
    public string? Url { get; set; }

    [SugarColumn(ColumnName = "CONTENT_ID_", IsNullable = true)]
    public string? ContentId { get; set; }

    [SugarColumn(ColumnName = "TIME_", IsNullable = true)]
    public DateTime? CreateTime { get; set; }
}
