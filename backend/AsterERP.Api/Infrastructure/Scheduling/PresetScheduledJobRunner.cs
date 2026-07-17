using AsterERP.Api.Modules.System.Logs;
using AsterERP.Api.Modules.System.Parameters;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Scheduling;

public sealed class PresetScheduledJobRunner(ISqlSugarClient db)
{
    public async Task<string> RunAsync(string presetJobCode, CancellationToken cancellationToken = default)
    {
        return presetJobCode switch
        {
            "system.health-check" => await RunHealthCheckAsync(cancellationToken),
            "system.parameter-inspection" => await RunParameterInspectionAsync(cancellationToken),
            "system.operation-log-inspection" => await RunOperationLogInspectionAsync(cancellationToken),
            _ => throw new InvalidOperationException("预置任务不存在")
        };
    }

    private async Task<string> RunHealthCheckAsync(CancellationToken cancellationToken)
    {
        var parameterCount = await db.Queryable<SystemParameterEntity>().Where(item => !item.IsDeleted).CountAsync(cancellationToken);
        return $"系统健康检查完成，参数表可访问，当前参数数量 {parameterCount}";
    }

    private async Task<string> RunParameterInspectionAsync(CancellationToken cancellationToken)
    {
        var total = await db.Queryable<SystemParameterEntity>().Where(item => !item.IsDeleted).CountAsync(cancellationToken);
        var enabled = await db.Queryable<SystemParameterEntity>().Where(item => !item.IsDeleted && item.IsEnabled).CountAsync(cancellationToken);
        return $"系统参数巡检完成，总数 {total}，启用 {enabled}，停用 {total - enabled}";
    }

    private async Task<string> RunOperationLogInspectionAsync(CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow.AddDays(-1);
        var total = await db.Queryable<SystemOperationLogEntity>().Where(item => !item.IsDeleted && item.CreatedTime >= start).CountAsync(cancellationToken);
        var success = await db.Queryable<SystemOperationLogEntity>().Where(item => !item.IsDeleted && item.CreatedTime >= start && item.IsSuccess).CountAsync(cancellationToken);
        return $"最近 24 小时操作日志 {total} 条，成功 {success} 条";
    }
}
