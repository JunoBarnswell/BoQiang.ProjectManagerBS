namespace AsterERP.Api.Application.Runtime;

public interface IDataModelProvider
{
    string ProviderKey { get; }

    Task<RuntimeDataModelQueryResult> QueryAsync(
        RuntimeDataModelDefinition model,
        RuntimeDataModelQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, object?>?> GetDetailAsync(
        RuntimeDataModelDefinition model,
        string id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, object?>?> CreateAsync(
        RuntimeDataModelDefinition model,
        IReadOnlyList<RuntimeDataModelFieldUpdate> values,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateFieldsAsync(
        RuntimeDataModelDefinition model,
        string id,
        IReadOnlyList<RuntimeDataModelFieldUpdate> updates,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        RuntimeDataModelDefinition model,
        string id,
        CancellationToken cancellationToken = default);
}
