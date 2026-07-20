using System.Globalization;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// Project-management text is kept as semantic keys so persisted events never
/// depend on the language of the actor who created them.
/// </summary>
public static class ProjectManagementText
{
    private static readonly IReadOnlyDictionary<string, string> ZhCn = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["projectManagement.api.notification.notFound"] = "通知不存在",
        ["projectManagement.api.notification.contextMismatch"] = "通知上下文与当前会话不一致",
        ["projectManagement.api.notification.required"] = "通知字段不能为空",
        ["projectManagement.api.notification.open.auditDenied"] = "无权访问关联操作记录",
        ["projectManagement.api.notification.open.syncDenied"] = "无权访问项目同步",
        ["projectManagement.api.notification.open.unavailable"] = "通知未关联可打开的项目对象",
        ["projectManagement.api.notification.open.targetUnavailable"] = "关联项目或任务已删除，或你已无权访问",
        ["projectManagement.api.session.tenantMissing"] = "当前会话缺少租户",
        ["projectManagement.api.session.appMissing"] = "当前会话缺少应用",
        ["projectManagement.api.session.userMissing"] = "当前会话缺少用户",
        ["projectManagement.api.task.statusUpdateFailed"] = "任务状态更新失败，请刷新后重试",
        ["projectManagement.api.internalError"] = "系统繁忙，请稍后重试"
    };

    private static readonly IReadOnlyDictionary<string, string> EnUs = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["projectManagement.api.notification.notFound"] = "Notification was not found.",
        ["projectManagement.api.notification.contextMismatch"] = "The notification context does not match the current session.",
        ["projectManagement.api.notification.required"] = "A required notification field is missing.",
        ["projectManagement.api.notification.open.auditDenied"] = "You do not have permission to view the related operation.",
        ["projectManagement.api.notification.open.syncDenied"] = "You do not have permission to view project synchronization.",
        ["projectManagement.api.notification.open.unavailable"] = "This notification is not associated with an available project object.",
        ["projectManagement.api.notification.open.targetUnavailable"] = "The related project or task was deleted, or you no longer have access.",
        ["projectManagement.api.session.tenantMissing"] = "The current session does not include a tenant.",
        ["projectManagement.api.session.appMissing"] = "The current session does not include an application.",
        ["projectManagement.api.session.userMissing"] = "The current session does not include a user.",
        ["projectManagement.api.task.statusUpdateFailed"] = "The task status could not be updated. Refresh and try again.",
        ["projectManagement.api.internalError"] = "The service is busy. Try again later."
    };

    public static string Resolve(string key, IReadOnlyDictionary<string, string>? arguments = null)
    {
        var catalog = string.Equals(CultureInfo.CurrentUICulture.Name, "en-US", StringComparison.OrdinalIgnoreCase) ? EnUs : ZhCn;
        var template = catalog.TryGetValue(key, out var value) ? value : key;
        if (arguments is null || arguments.Count == 0) return template;
        foreach (var argument in arguments) template = template.Replace($"{{{argument.Key}}}", argument.Value, StringComparison.Ordinal);
        return template;
    }
}
