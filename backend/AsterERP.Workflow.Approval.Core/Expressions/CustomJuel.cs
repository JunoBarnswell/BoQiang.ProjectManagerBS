using AsterERP.Workflow.Core;

namespace AsterERP.Workflow.Approval.Core.Expressions;

[Serializable]
public class CustomJuel
{
    public List<string> ToList(params string[] datas)
    {
        var dataSet = new HashSet<string>();
        if (datas.Length > 0)
        {
            foreach (var data in datas)
            {
                dataSet.Add(data);
            }
        }
        return dataSet.ToList();
    }

    public bool Deq(string date)
    {
        if (!string.IsNullOrWhiteSpace(date))
        {
            var currentDate = AbpTimeIdProvider.UtcNow.ToString("yyyy-MM-dd");
            if (currentDate == date)
            {
                return true;
            }
        }
        return false;
    }

    public bool Dlt(string date, int num)
    {
        if (!string.IsNullOrWhiteSpace(date))
        {
            if (DateTime.TryParse(date, out var parsedDate))
            {
                var between = (AbpTimeIdProvider.UtcNow.Date - parsedDate.Date).Days;
                if (Math.Abs(between) < num)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public bool Dlte(string date, int num)
    {
        if (!string.IsNullOrWhiteSpace(date))
        {
            if (DateTime.TryParse(date, out var parsedDate))
            {
                var between = (AbpTimeIdProvider.UtcNow.Date - parsedDate.Date).Days;
                if (Math.Abs(between) <= num)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public bool Dgt(string date, int num)
    {
        if (!string.IsNullOrWhiteSpace(date))
        {
            if (DateTime.TryParse(date, out var parsedDate))
            {
                var between = (AbpTimeIdProvider.UtcNow.Date - parsedDate.Date).Days;
                if (Math.Abs(between) > num)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public bool Dgte(string date, int num)
    {
        if (!string.IsNullOrWhiteSpace(date))
        {
            if (DateTime.TryParse(date, out var parsedDate))
            {
                var between = (AbpTimeIdProvider.UtcNow.Date - parsedDate.Date).Days;
                if (Math.Abs(between) >= num)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
