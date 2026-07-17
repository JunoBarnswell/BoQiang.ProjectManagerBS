using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_PROCDEF_INFO")]
public class ProcessDefinitionInfoEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = AbpTimeIdProvider.NewGuid("N");

    [SugarColumn(ColumnName = "PROC_DEF_ID_", IsNullable = true)]
    public string? ProcessDefinitionId { get; set; }

    [SugarColumn(ColumnName = "REV_")]
    public int Revision { get; set; } = 1;

    [SugarColumn(ColumnName = "INFO_JSON_ID_", IsNullable = true)]
    public string? InfoJsonId { get; set; }

    [SugarColumn(IsIgnore = true)]
    public string? InfoJson { get; set; }
}

