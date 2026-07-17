using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Forms.Api.Models.Form;

[SqlSugar.SugarTable("tbl_form_form_info")]
public class FormInfo : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? CategoryCode { get; set; }

    public string? Title { get; set; }

    public string? Code { get; set; }

    public string? Name { get; set; }

    public string? Content { get; set; }

    public int? FormStatus { get; set; }

    public string? Version { get; set; }

    public string? FormJson { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public new string? Keyword { get; set; }
}
