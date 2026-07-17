namespace AsterERP.Workflow.Approval.Api.Models.Privilege;

[SqlSugar.SugarTable("tbl_shiro_session")]
public class ShiroSession
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? Session { get; set; }

    public ShiroSession() { }

    public ShiroSession(string? id, string? session)
    {
        Id = id;
        Session = session;
    }
}
