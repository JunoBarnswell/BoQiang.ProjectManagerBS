namespace AsterERP.Contracts.ProjectManagement;

/// <summary>甘特图依赖分析的只读快照。所有日期均为项目任务计划日期，系统不会自动改写排程。</summary>
public sealed record ProjectManagementTaskDependencyAnalysisResponse(
    IReadOnlyList<ProjectManagementTaskDependencyAnalysisTask> Tasks,
    IReadOnlyList<ProjectManagementTaskDependencyAnalysisLink> Links,
    IReadOnlyList<ProjectManagementTaskDependencyAnalysisMilestoneImpact> MilestoneImpacts,
    IReadOnlyList<ProjectManagementTaskDependencyAnalysisDiagnostic> Diagnostics,
    DateTime? ProjectEarliestFinish);

public sealed record ProjectManagementTaskDependencyAnalysisTask(
    string TaskId,
    string Title,
    string? MilestoneId,
    DateTime? PlannedStart,
    DateTime? PlannedFinish,
    DateTime? EarliestStart,
    DateTime? EarliestFinish,
    DateTime? LatestStart,
    DateTime? LatestFinish,
    int? TotalFloatMinutes,
    bool IsCritical,
    bool IsSchedulable);

public sealed record ProjectManagementTaskDependencyAnalysisLink(
    string DependencyId,
    string PredecessorTaskId,
    string SuccessorTaskId,
    string DependencyType,
    int LagMinutes,
    bool IsRenderable,
    bool IsCritical);

public sealed record ProjectManagementTaskDependencyAnalysisMilestoneImpact(
    string MilestoneId,
    string Name,
    DateTime? DueDate,
    DateTime? ForecastFinish,
    int DelayMinutes,
    bool IsAtRisk,
    IReadOnlyList<string> AffectedTaskIds);

public sealed record ProjectManagementTaskDependencyAnalysisDiagnostic(
    string Code,
    string Severity,
    string Message,
    IReadOnlyList<string> TaskIds,
    string? DependencyId = null);

/// <summary>
/// 仅用于预览拖动/改日期的影响。该命令绝不保存任务，调用方必须将建议交给用户确认后再走原有任务更新命令。
/// </summary>
public sealed record ProjectManagementTaskDependencyImpactPreviewRequest(
    string TaskId,
    DateTime? ProposedStartDate,
    DateTime? ProposedDueDate);

public sealed record ProjectManagementTaskDependencyImpactPreviewResponse(
    ProjectManagementTaskDependencyAnalysisResponse Baseline,
    ProjectManagementTaskDependencyAnalysisResponse Preview,
    IReadOnlyList<ProjectManagementTaskDependencyScheduleSuggestion> Suggestions);

public sealed record ProjectManagementTaskDependencyScheduleSuggestion(
    string TaskId,
    string Title,
    DateTime CurrentStart,
    DateTime SuggestedStart,
    DateTime CurrentFinish,
    DateTime SuggestedFinish,
    int ShiftMinutes,
    bool RequiresManualConfirmation = true);
