using AsterERP.Contracts.ApplicationConsole;

namespace AsterERP.Api.Application.ApplicationConsole;

public interface IApplicationConsoleService
{
    Task<ApplicationConsoleSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<ApplicationDatabaseBindingStatusResponse> GetDatabaseBindingStatusAsync(CancellationToken cancellationToken = default);

    Task<ApplicationDatabaseBindingResponse> TestDatabaseBindingAsync(
        ApplicationDatabaseBindingRequest request,
        CancellationToken cancellationToken = default);

    Task<ApplicationDatabaseBindingResponse> SaveDatabaseBindingAsync(
        ApplicationDatabaseBindingRequest request,
        CancellationToken cancellationToken = default);
}
