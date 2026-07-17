using AsterERP.Shared;

namespace AsterERP.Api.Infrastructure.Dicts;

public sealed record DictionaryOptionsCacheItem(IReadOnlyList<OptionItem> Options);
