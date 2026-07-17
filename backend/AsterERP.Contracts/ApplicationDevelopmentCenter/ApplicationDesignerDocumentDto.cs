using System.Text.Json;

namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed record ApplicationDesignerDocumentDto(
    string DocumentId,
    int Revision,
    string DocumentHash,
    JsonElement Metadata,
    JsonElement Pages,
    JsonElement Elements,
    JsonElement Actions,
    JsonElement ApiBindings,
    JsonElement DataSources,
    JsonElement Modals,
    JsonElement PageMicroflows,
    JsonElement PageParameters,
    JsonElement Permissions,
    JsonElement StyleTokens,
    JsonElement Variables,
    JsonElement WorkflowBindings,
    JsonElement RuntimeContext);
