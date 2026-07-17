using AsterERP.Contracts.ApplicationConsole;

namespace AsterERP.Contracts.Auth;

public sealed record ApplicationLoginBootstrapResponse(
    string TenantId,
    string TenantName,
    string AppCode,
    string AppName,
    string SystemName,
    string Status,
    ApplicationDatabaseBindingStatusResponse DatabaseBinding);
