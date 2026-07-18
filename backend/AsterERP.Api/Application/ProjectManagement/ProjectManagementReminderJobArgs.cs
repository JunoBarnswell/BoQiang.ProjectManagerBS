namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// Hangfire 仅保存工作区和提醒标识；正文、备注及接收内容均在执行时从项目数据库读取。
/// </summary>
public sealed record ProjectManagementReminderJobArgs(
    string ReminderId,
    string TenantId,
    string AppCode,
    string RecipientUserId,
    long VersionNo);
