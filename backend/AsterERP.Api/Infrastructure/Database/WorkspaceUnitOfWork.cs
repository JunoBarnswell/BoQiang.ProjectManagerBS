using AsterERP.Api.Infrastructure.UnitOfWork;

namespace AsterERP.Api.Infrastructure.Database;

public sealed class WorkspaceUnitOfWork(IWorkspaceDatabaseAccessor databaseAccessor) : IUnitOfWork
{
    public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var db = databaseAccessor.GetCurrentDb();
        db.Ado.BeginTran();
        try
        {
            await action();
            cancellationToken.ThrowIfCancellationRequested();
            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var db = databaseAccessor.GetCurrentDb();
        db.Ado.BeginTran();
        try
        {
            var result = await action();
            cancellationToken.ThrowIfCancellationRequested();
            db.Ado.CommitTran();
            return result;
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
    }
}
