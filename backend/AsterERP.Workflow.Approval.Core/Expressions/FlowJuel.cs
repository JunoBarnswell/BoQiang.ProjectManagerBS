namespace AsterERP.Workflow.Approval.Core.Expressions;

[Serializable]
public class FlowJuel
{
    public string One(string applyers)
    {
        var applyer = "";
        if (!string.IsNullOrWhiteSpace(applyers))
        {
            var userSet = new HashSet<string>();
            var userCodes = applyers.Split(',');
            foreach (var userCode in userCodes)
            {
                if (!string.IsNullOrWhiteSpace(userCode))
                {
                    userSet.Add(userCode.Trim());
                }
            }
            if (userSet.Count > 0)
            {
                applyer = userSet.First();
            }
        }
        return applyer;
    }

    public List<string> Multi(params string[] applyers)
    {
        var applyerList = new HashSet<string>();
        if (applyers != null && applyers.Length > 0)
        {
            foreach (var applyer in applyers)
            {
                if (!string.IsNullOrWhiteSpace(applyer))
                {
                    GetApplerCodes(applyerList, applyer);
                }
            }
        }
        return applyerList.ToList();
    }

    private static void GetApplerCodes(HashSet<string> applyerList, string applyers)
    {
        if (string.IsNullOrWhiteSpace(applyers)) return;
        var codes = new HashSet<string>();
        var userCodes = applyers.Split(',');
        foreach (var userCode in userCodes)
        {
            if (!string.IsNullOrWhiteSpace(userCode))
            {
                codes.Add(userCode.Trim());
            }
        }
        if (codes.Count > 0)
        {
            applyerList.UnionWith(codes);
        }
    }

    public bool Exist(string str, string searchStr)
    {
        if (!string.IsNullOrWhiteSpace(str) && !string.IsNullOrWhiteSpace(searchStr))
        {
            return str.Contains(searchStr);
        }
        return false;
    }
}
