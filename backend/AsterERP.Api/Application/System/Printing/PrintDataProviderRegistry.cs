namespace AsterERP.Api.Application.System.Printing;

public sealed class PrintDataProviderRegistry(IEnumerable<IPrintDataProvider> providers)
{
    private readonly IReadOnlyDictionary<string, IPrintDataProvider> _providerMap =
        providers.ToDictionary(provider => provider.Key, StringComparer.OrdinalIgnoreCase);

    public IPrintDataProvider GetRequired(string key)
    {
        if (_providerMap.TryGetValue(key, out var provider))
        {
            return provider;
        }

        throw new KeyNotFoundException($"未找到打印详情提供器：{key}");
    }
}
