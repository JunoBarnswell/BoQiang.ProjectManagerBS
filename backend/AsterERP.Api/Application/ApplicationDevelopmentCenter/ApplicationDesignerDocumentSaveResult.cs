using AsterERP.Api.Modules.ApplicationDevelopmentCenter;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter;

public sealed record ApplicationDesignerDocumentSaveResult(
    ApplicationDesignerDocumentEntity Document,
    bool CreatedRevision,
    string? RevisionId,
    string DocumentHash);
