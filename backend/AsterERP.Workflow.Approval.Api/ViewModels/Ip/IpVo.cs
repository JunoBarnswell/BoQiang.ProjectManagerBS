namespace AsterERP.Workflow.Approval.Api.ViewModels.Ip;

public class IpVo
{
    public string Country { get; set; }
    public string Region { get; set; }
    public string City { get; set; }

    public IpVo() { }

    public IpVo(string country, string region, string city)
    {
        Country = country;
        Region = region;
        City = city;
    }
}
