namespace AsterERP.Contracts.ApplicationConsole;

public sealed record ApplicationDatabaseBindingResponse(
    bool IsBound,
    bool IsReachable,
    string? Provider,
    string? DisplayName,
    string? DatabaseName,
    DateTime? UpdatedAt,
    bool CanManage,
    string Message,
    string Status = ApplicationDatabaseBindingStatus.Ready);
