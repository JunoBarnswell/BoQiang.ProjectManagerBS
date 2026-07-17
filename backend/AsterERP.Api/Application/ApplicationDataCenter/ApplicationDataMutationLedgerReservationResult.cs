using AsterERP.Api.Modules.ApplicationDataCenter;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed record ApplicationDataMutationLedgerReservationResult(
    ApplicationDataMutationLedgerEntity Ledger,
    bool IsNew);
