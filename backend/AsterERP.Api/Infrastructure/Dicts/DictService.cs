using AsterERP.Shared;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Modules.System.Dicts;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace AsterERP.Api.Infrastructure.Dicts;

public sealed class DictService(
    IRepository<SystemDictTypeEntity> dictTypeRepository,
    IRepository<SystemDictItemEntity> dictItemRepository,
    IDistributedCache cache,
    ILogger<DictService> logger) : IDictService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public async Task<IReadOnlyList<OptionItem>> GetOptionsAsync(string dictCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dictCode))
        {
            return [];
        }

        var cacheKey = GetCacheKey(dictCode);
        var cachedBytes = await cache.GetAsync(cacheKey, cancellationToken);
        var cachedItem = cachedBytes is null
            ? null
            : JsonSerializer.Deserialize<DictionaryOptionsCacheItem>(cachedBytes);
        if (cachedItem is not null)
        {
            return cachedItem.Options;
        }

        var dictType = await dictTypeRepository.FirstOrDefaultAsync(item => item.DictCode == dictCode, cancellationToken);
        if (dictType is null || !dictType.IsEnabled)
        {
            logger.LogWarning("Dictionary type not found: {DictCode}", dictCode);
            return [];
        }

        var items = await dictItemRepository.ListAsync(item => item.DictTypeId == dictType.Id, cancellationToken: cancellationToken);
        var options = items
            .Where(item => item.IsEnabled)
            .OrderBy(item => item.SortOrder)
            .Select(item => new OptionItem(item.ItemLabel, item.ItemValue))
            .ToList();

        await cache.SetAsync(
            cacheKey,
            JsonSerializer.SerializeToUtf8Bytes(new DictionaryOptionsCacheItem(options)),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheDuration },
            cancellationToken);
        return options;
    }

    private static string GetCacheKey(string dictCode)
    {
        return $"dict-options::{dictCode.ToLowerInvariant()}";
    }
}
