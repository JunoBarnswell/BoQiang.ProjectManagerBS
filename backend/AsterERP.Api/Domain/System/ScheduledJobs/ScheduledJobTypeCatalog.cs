using AsterERP.Contracts.System.ScheduledJobs;

namespace AsterERP.Api.Domain.System.ScheduledJobs;

public sealed class ScheduledJobTypeCatalog
{
    private static readonly ScheduledJobTypeOptionResponse[] Presets =
    [
        new("system.health-check", "系统健康检查", "检查应用服务、数据库连接和运行时状态。", false),
        new("system.parameter-inspection", "系统参数巡检", "统计系统参数数量和启用状态，形成运行巡检日志。", false),
        new("system.operation-log-inspection", "操作日志巡检", "统计最近操作日志数量和成功率，用于运维观察。", false)
    ];

    public IReadOnlyList<ScheduledJobTypeOptionResponse> GetPresetJobs() => Presets;

    public bool ContainsPreset(string presetJobCode)
    {
        return Presets.Any(item => string.Equals(item.Code, presetJobCode, StringComparison.OrdinalIgnoreCase));
    }

    public ScheduledJobTypeOptionResponse RequirePreset(string presetJobCode)
    {
        return Presets.FirstOrDefault(item => string.Equals(item.Code, presetJobCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("预置任务不存在");
    }
}
