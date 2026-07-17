using System.Text.Json;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.System.ScheduledJobs;

namespace AsterERP.Api.Domain.System.ScheduledJobs;

public sealed class ScheduledJobDomainPolicy(
    ScheduledJobTypeCatalog typeCatalog,
    ScheduleExpressionBuilder scheduleExpressionBuilder,
    HttpCallbackDomainPolicy httpCallbackDomainPolicy)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SupportedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ScheduledJobConstants.StatusEnabled,
        ScheduledJobConstants.StatusPaused
    };

    public ScheduleBuildResult EnsureUpsertRequest(ScheduledJobUpsertRequest request, SchedulerOptions schedulerOptions)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
        {
            throw new ValidationException("请填写任务名称和任务编码", ErrorCodes.ScheduledJobInvalid);
        }

        if (!SupportedStatuses.Contains(request.Status.Trim()))
        {
            throw new ValidationException("任务状态无效", ErrorCodes.ScheduledJobInvalid);
        }

        var jobType = request.JobType.Trim();
        if (jobType == ScheduledJobConstants.JobTypePreset)
        {
            if (string.IsNullOrWhiteSpace(request.PresetJobCode) || !typeCatalog.ContainsPreset(request.PresetJobCode.Trim()))
            {
                throw new ValidationException("请选择有效的预置任务", ErrorCodes.ScheduledJobInvalid);
            }
        }
        else if (jobType == ScheduledJobConstants.JobTypeHttpCallback)
        {
            httpCallbackDomainPolicy.EnsureAllowed(request.HttpCallback, schedulerOptions);
        }
        else
        {
            throw new ValidationException("任务类型无效", ErrorCodes.ScheduledJobInvalid);
        }

        if (!string.IsNullOrWhiteSpace(request.Parameters))
        {
            EnsureJson(request.Parameters);
        }

        return scheduleExpressionBuilder.Build(request.Schedule);
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    public static T? Deserialize<T>(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(value, JsonOptions);
    }

    private static void EnsureJson(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw new ValidationException("任务参数必须是合法 JSON", ErrorCodes.ScheduledJobInvalid);
        }
    }
}
