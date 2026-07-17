namespace AsterERP.Contracts.Workflows;

public sealed record WorkflowReportOverviewResponse(
    WorkflowApprovalStatisticsResponse ApprovalStatistics,
    WorkflowEfficiencyAnalysisResponse EfficiencyAnalysis,
    IReadOnlyList<WorkflowBusinessDataReportItemResponse> BusinessData);

public sealed record WorkflowApprovalStatisticsResponse(
    int TotalStarted,
    int Running,
    int Completed,
    int Rejected,
    int Withdrawn,
    int Terminated,
    int Todo,
    int Done,
    int Cc);

public sealed record WorkflowEfficiencyAnalysisResponse(
    double AverageDurationHours,
    int OverdueTaskCount,
    IReadOnlyList<WorkflowBottleneckNodeResponse> BottleneckNodes);

public sealed record WorkflowBottleneckNodeResponse(
    string NodeKey,
    string NodeName,
    int CompletedCount,
    double AverageDurationHours);

public sealed record WorkflowBusinessDataReportItemResponse(
    string BusinessType,
    int Total,
    int Running,
    int Finished);
