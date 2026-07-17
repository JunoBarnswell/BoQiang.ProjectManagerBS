using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Forms.Api.Models.Form;

[SqlSugar.SugarTable("tbl_form_form_data_info")]
public class FormDataInfo : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? ModelKey { get; set; }

    public string? BusinessKey { get; set; }

    public string? ProcessInstanceId { get; set; }

    public string? FormData { get; set; }
}
