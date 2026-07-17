using SqlSugar;

namespace AsterERP.Api.Infrastructure.UnitOfWork;

public sealed class SqlSugarUnitOfWork(ISqlSugarClient db) : IUnitOfWork
{
    public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
