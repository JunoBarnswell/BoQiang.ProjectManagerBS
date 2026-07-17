namespace AsterERP.Workflow.Approval.Api.ViewModels.Token;

public class AuthTokenVo
{
    public string AppSn { get; set; }
    public string AppSecretKey { get; set; }

    public AuthTokenVo() { }

    public AuthTokenVo(string appSn, string appSecretKey)
    {
        AppSn = appSn;
        AppSecretKey = appSecretKey;
    }
}
