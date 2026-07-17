using System.Text.Json;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.System.QueryViews;

namespace AsterERP.Api.Application.System.QueryViews;

public static class QueryViewDesignJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static string Serialize(QueryViewDesignerSaveRequest request)
    {
        return JsonSerializer.Serialize(request, Options);
    }

    public static QueryViewDesignerSaveRequest Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<QueryViewDesignerSaveRequest>(json, Options)
                ?? throw new ValidationException("视图配置不能为空");
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"视图配置 JSON 不合法: {ex.Message}");
        }
    }
}
