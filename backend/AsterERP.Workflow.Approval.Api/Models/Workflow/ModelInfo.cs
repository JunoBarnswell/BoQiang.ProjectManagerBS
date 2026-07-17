using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Workflow;

[SqlSugar.SugarTable("tbl_flow_model_info")]
public class ModelInfo : BaseModel
{
    [SqlSugar.SugarColumn(IsIgnore = true)]
    public new string Keyword { get; set; }

    public const int CUSTOM_MODEL_TYPE = 0;
    public const int BIZ_MODEL_TYPE = 1;

    public string Id { get; set; }
    public string ModelId { get; set; }
    public string Name { get; set; }
    public string ModelKey { get; set; }
    public int? ModelType { get; set; }
    public int? FormType { get; set; }
    public string AppSn { get; set; }
    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string AppName { get; set; }
    public string CategoryCode { get; set; }
    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string CategoryName { get; set; }
    public int? Status { get; set; }
    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string StatusName { get; set; }
    public int? ExtendStatus { get; set; }
    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string ExtendStatusName { get; set; }
    public string OwnDeptId { get; set; }
    public string OwnDeptName { get; set; }
    public string FlowOwnerNo { get; set; }
    public string FlowOwnerName { get; set; }
    public string ProcessDockingNo { get; set; }
    public string ProcessDockingName { get; set; }
    public string ApplyCompanies { get; set; }
    public string ShowStatus { get; set; }
    public int? AppliedRange { get; set; }
    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string AppliedRangeName { get; set; }
    public string AuthPointList { get; set; }
    public string Superuser { get; set; }
    public string BusinessUrl { get; set; }
    public int? SkipSet { get; set; }
    public string ModelIcon { get; set; }
    public int? OrderNo { get; set; }
    [SqlSugar.SugarColumn(IsIgnore = true)]
    public List<string> CategoryCodes { get; set; }
    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string ProcessDefinitionId { get; set; }
    [SqlSugar.SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string ModelXml { get; set; }
    [SqlSugar.SugarColumn(IsIgnore = true)]
    public int? Version { get; set; }
    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string CompanyId { get; set; }
}
