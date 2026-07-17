namespace AsterERP.Api.Application.Runtime;

public interface ITransactionalDataModelProvider : IDataModelProvider
{
    Task<T> ExecuteInTransactionAsync<T>(
        IReadOnlyList<RuntimeDataModelDefinition> models,
        Func<Task<T>> action,
        CancellationToken cancellationToken = default);
}
