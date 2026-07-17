using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Runtime;

public sealed class DataModelProviderRegistry(IEnumerable<IDataModelProvider> providers) : IDataModelProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IDataModelProvider> providerMap = providers
        .GroupBy(item => item.ProviderKey, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(item => item.Key, item => item.First(), StringComparer.OrdinalIgnoreCase);

    public IDataModelProvider GetRequired(string providerKey)
    {
        if (providerMap.TryGetValue(providerKey, out var provider))
        {
            return provider;
        }

        throw new ValidationException("运行时数据模型 Provider 未注册", ErrorCodes.RuntimeDataModelInvalid);
    }
}
