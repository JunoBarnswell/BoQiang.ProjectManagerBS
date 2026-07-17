using System.Text.Json;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataMutationLedgerServiceTests : IDisposable
{
    private readonly ApplicationDataStudioSqliteFixture fixture = new();

    [Fact]
    public async Task ConcurrentReservationsShareOneDatabaseUniqueLedger()
    {
        var request = CreateRequest("concurrent-request");
        var clients = Enumerable.Range(0, 8)
            .Select(_ => ApplicationDataStudioSqliteFixture.CreateDb(fixture.ApplicationDatabasePath))
            .ToArray();
        var services = clients
            .Select(client => new ApplicationDataMutationLedgerService(
                new TestWorkspaceDatabaseAccessor(client),
                fixture.WorkspaceResolver))
            .ToArray();

        try
        {
            var reservations = await Task.WhenAll(services.Select(service => service.ReserveAsync(request)));

            Assert.Single(reservations, item => item.IsNew);
            Assert.Equal(7, reservations.Count(item => !item.IsNew));
            Assert.Single(reservations.Select(item => item.Ledger.Id).Distinct(StringComparer.Ordinal));
            Assert.Equal(1, fixture.AppDb.Ado.GetInt("SELECT COUNT(1) FROM app_data_mutation_ledgers"));
            Assert.Equal(1, fixture.AppDb.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'index' AND name = 'ux_app_data_mutation_ledger_request'"));
        }
        finally
        {
            foreach (var client in clients)
                client.Dispose();
        }
    }

    [Fact]
    public async Task ActiveExecutingReplayReturnsPersistedExecutingWithoutStartingAnotherExecution()
    {
        var service = new ApplicationDataMutationLedgerService(fixture.DatabaseAccessor, fixture.WorkspaceResolver);
        var request = CreateRequest("active-request");

        var first = await service.ReserveAsync(request);
        var replay = await service.ReserveAsync(request);

        Assert.True(first.IsNew);
        Assert.False(replay.IsNew);
        Assert.Equal(ApplicationDataMutationLedgerStatus.Executing, replay.Ledger.Status);
        Assert.False(replay.IsNew);
        Assert.NotNull(replay.Ledger.LeaseExpiresAt);
        Assert.Equal(first.Ledger.LeaseToken, replay.Ledger.LeaseToken);
        Assert.Equal(ApplicationDataMutationLedgerStatus.Executing,
            fixture.AppDb.Ado.GetString("SELECT Status FROM app_data_mutation_ledgers WHERE Id = '" + first.Ledger.Id + "'"));

        await Assert.ThrowsAsync<ValidationException>(() => service.ReconcileAsync(new ApplicationDataMutationLedgerReconcileRequest
        {
            LedgerId = first.Ledger.Id,
            TargetStatus = ApplicationDataMutationLedgerStatus.Finalized,
            BusinessEvidence = "active execution must not be reconciled"
        }));
    }

    [Fact]
    public async Task ExpiredExecutingLeaseBecomesRecoveryRequiredAndBlocksAutomaticTransition()
    {
        var service = new ApplicationDataMutationLedgerService(fixture.DatabaseAccessor, fixture.WorkspaceResolver);
        var request = CreateRequest("expired-request");
        var reservation = await service.ReserveAsync(request);

        fixture.AppDb.Ado.ExecuteCommand(
            "UPDATE app_data_mutation_ledgers SET LeaseExpiresAt = '2000-01-01T00:00:00Z' WHERE Id = '" + reservation.Ledger.Id + "'");

        var recovered = await service.GetAsync(reservation.Ledger.Id);

        Assert.Equal(ApplicationDataMutationLedgerStatus.RecoveryRequired, recovered.Status);
        Assert.Equal("ExecutionLeaseExpired", recovered.FailureCode);
        Assert.Contains("Automatic retry is forbidden", recovered.StatusReason, StringComparison.Ordinal);
        Assert.Equal(0, fixture.AppDb.Ado.GetInt("SELECT COUNT(1) FROM app_data_mutation_ledgers WHERE LeaseExpiresAt IS NOT NULL AND Status = 'RecoveryRequired'"));

        await Assert.ThrowsAsync<ValidationException>(() => service.TransitionAsync(
            reservation.Ledger.Id,
            ApplicationDataMutationLedgerStatus.Finalized,
            1,
            null,
            null,
            "stale executor must not finalize after lease expiry"));
    }

    [Fact]
    public async Task UnknownOutcomeRequiresManualRecoveryAndReconcileIsIdempotent()
    {
        var service = new ApplicationDataMutationLedgerService(fixture.DatabaseAccessor, fixture.WorkspaceResolver);
        var reservation = await service.ReserveAsync(CreateRequest("unknown-request"));

        await service.TransitionAsync(
            reservation.Ledger.Id,
            ApplicationDataMutationLedgerStatus.Unknown,
            0,
            "ExternalWriteUnknown",
            "connection closed after external command started",
            "External write outcome is unknown; manual recovery is required.");

        var first = await service.ReconcileAsync(new ApplicationDataMutationLedgerReconcileRequest
        {
            LedgerId = reservation.Ledger.Id,
            TargetStatus = ApplicationDataMutationLedgerStatus.Finalized,
            ExternalAffectedRows = 1,
            BusinessEvidence = "external row count and business receipt verified"
        });
        var replay = await service.ReconcileAsync(new ApplicationDataMutationLedgerReconcileRequest
        {
            LedgerId = reservation.Ledger.Id,
            TargetStatus = ApplicationDataMutationLedgerStatus.Finalized,
            ExternalAffectedRows = 1,
            BusinessEvidence = "external row count and business receipt verified"
        });

        Assert.Equal(ApplicationDataMutationLedgerStatus.Finalized, first.ConfirmedStatus);
        Assert.Equal(first.Ledger.LedgerId, replay.Ledger.LedgerId);
        Assert.Equal(ApplicationDataMutationLedgerStatus.Finalized, replay.Ledger.Status);
        Assert.Equal(1, replay.ExternalAffectedRows);
        Assert.Contains("separate transaction boundaries", replay.Ledger.StatusReason, StringComparison.OrdinalIgnoreCase);

        var historyJson = fixture.AppDb.Ado.GetString(
            "SELECT StatusHistoryJson FROM app_data_mutation_ledgers WHERE Id = '" + reservation.Ledger.Id + "'");
        using var history = JsonDocument.Parse(historyJson);
        Assert.Equal(
            new[] { ApplicationDataMutationLedgerStatus.Executing, ApplicationDataMutationLedgerStatus.Unknown, ApplicationDataMutationLedgerStatus.Finalized },
            history.RootElement.EnumerateArray().Select(item => item.GetProperty("Status").GetString()).ToArray());

        await Assert.ThrowsAsync<ValidationException>(() => service.ReconcileAsync(new ApplicationDataMutationLedgerReconcileRequest
        {
            LedgerId = reservation.Ledger.Id,
            TargetStatus = ApplicationDataMutationLedgerStatus.Failed,
            BusinessEvidence = "a conflicting terminal decision is not idempotent"
        }));
    }

    [Fact]
    public async Task KnownFailureIsMarkedSafeToRetryButIsNeverAutomaticallyReplayed()
    {
        var service = new ApplicationDataMutationLedgerService(fixture.DatabaseAccessor, fixture.WorkspaceResolver);
        var request = CreateRequest("known-failure-request");
        var reservation = await service.ReserveAsync(request);

        await service.TransitionAsync(
            reservation.Ledger.Id,
            ApplicationDataMutationLedgerStatus.Failed,
            0,
            "ExternalWriteFailed",
            "transaction rolled back before external commit",
            "Known external failure.");

        var state = await service.GetAsync(reservation.Ledger.Id);
        var replay = await service.ReserveAsync(request);

        Assert.Equal(ApplicationDataMutationLedgerStatus.Failed, state.Status);
        Assert.Contains("SafeRetryDisposition", state.StatusReason, StringComparison.Ordinal);
        Assert.Contains("exactly-once is not claimed", state.StatusReason, StringComparison.OrdinalIgnoreCase);
        Assert.False(replay.IsNew);
        Assert.Equal(ApplicationDataMutationLedgerStatus.Failed, replay.Ledger.Status);
    }

    private static ApplicationDataMutationLedgerReservation CreateRequest(string requestHash) => new(
        "INSERT",
        requestHash,
        "table.row",
        "resource-1",
        "data-source-1",
        "dc_people",
        "INSERT",
        "statement-hash",
        "Sqlite",
        1);

    public void Dispose() => fixture.Dispose();
}
