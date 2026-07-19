namespace AsterERP.Contracts.ProjectManagement;

/// <summary>从受控整库导出记录恢复当前项目管理工作区。</summary>
public sealed record ProjectManagementDataSpaceImportRequest(
    string ExportId,
    string CurrentPassword,
    bool ConfirmRisk,
    string? Reason = null);

public sealed record ProjectManagementDataSpaceImportResponse(
    string OperationId,
    string ExportId,
    string Status,
    DateTime RequestedAt);
