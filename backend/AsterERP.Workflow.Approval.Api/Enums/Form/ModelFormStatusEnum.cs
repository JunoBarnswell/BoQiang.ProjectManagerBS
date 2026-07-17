namespace AsterERP.Workflow.Approval.Api.Enums.Form;

public enum ModelFormStatusEnum
{
    CG,
    DFB,
    YFB,
    TY
}

public static class ModelFormStatusEnumExtensions
{
    private static readonly Dictionary<ModelFormStatusEnum, (int Status, string Msg)> _map = new()
    {
        [ModelFormStatusEnum.CG] = (1, "草稿"),
        [ModelFormStatusEnum.DFB] = (2, "待发布"),
        [ModelFormStatusEnum.YFB] = (3, "已发布"),
        [ModelFormStatusEnum.TY] = (4, "停用")
    };

    public static int GetStatus(this ModelFormStatusEnum value) => _map[value].Status;
    public static string GetMsg(this ModelFormStatusEnum value) => _map[value].Msg;

    public static string? GetName(int? status)
    {
        if (status == null) return null;
        foreach (var kvp in _map)
            if (kvp.Value.Status == status.Value) return kvp.Value.Msg;
        return null;
    }

    public static ModelFormStatusEnum? GetEnum(int? status)
    {
        if (status == null) return null;
        foreach (var kvp in _map)
            if (kvp.Value.Status == status.Value) return kvp.Key;
        return null;
    }

    public static ModelFormStatusEnum? GetMinStatus(int? formStatus, int? modelStatus, int? extendStatus)
    {
        var statuses = new List<int>
        {
            formStatus ?? 1,
            modelStatus ?? 1,
            extendStatus ?? 1
        };
        statuses.Sort();
        return GetEnum(statuses[0]);
    }

    public static ModelFormStatusEnum? GetMinStatus(int? modelStatus, int? extendStatus)
    {
        var statuses = new List<int>
        {
            modelStatus ?? 1,
            extendStatus ?? 1
        };
        statuses.Sort();
        return GetEnum(statuses[0]);
    }
}
