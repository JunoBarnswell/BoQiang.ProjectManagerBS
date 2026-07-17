namespace AsterERP.Workflow.Approval.Api.ViewModels.Org;

public class OrgTreeVo
{
    public const string COMPANY_TYPE = "1";
    public const string DEPT_TYPE = "2";

    public string Id { get; set; }
    public string Pid { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string ShortName { get; set; }
    public string CompanyId { get; set; }
    public string CompanyName { get; set; }
    public string SourceType { get; set; }
    public string LeaderCode { get; set; }
    public string LeaderName { get; set; }

    public OrgTreeVo() { }

    public OrgTreeVo(string id, string pid, string name, string shortName, string sourceType)
    {
        Id = id;
        Pid = pid;
        Name = name;
        ShortName = shortName;
        SourceType = sourceType;
    }

    public OrgTreeVo(string id, string pid, string code, string name, string shortName, string companyId, string companyName, string sourceType, string leaderCode, string leaderName)
    {
        Id = id;
        Pid = pid;
        Code = code;
        Name = name;
        ShortName = shortName;
        CompanyId = companyId;
        CompanyName = companyName;
        SourceType = sourceType;
        LeaderCode = leaderCode;
        LeaderName = leaderName;
    }
}
