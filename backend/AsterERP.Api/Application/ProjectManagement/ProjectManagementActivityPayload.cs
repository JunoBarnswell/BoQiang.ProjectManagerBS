using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 写入 pm_activities.Remark 的版本化业务活动载荷。
/// </summary>
internal sealed record ProjectManagementActivityPayload(
    string Source,
    IReadOnlyList<ProjectManagementActivityFieldChange> FieldChanges,
    ProjectManagementActivityBatch? Batch,
    ProjectManagementLocalizedText? SummaryText = null);
