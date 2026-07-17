using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.QueryViews;

[SugarTable("system_query_view_user_preferences")]
public sealed class SystemQueryViewUserPreferenceEntity : EntityBase
{
    public string UserId { get; set; } = string.Empty;

    public string ViewCode { get; set; } = string.Empty;

    public string PreferenceJson { get; set; } = "{}";
}
