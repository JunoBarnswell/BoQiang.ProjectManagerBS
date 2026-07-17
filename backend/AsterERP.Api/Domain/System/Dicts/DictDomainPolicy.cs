using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Domain.System.Dicts;

public static class DictDomainPolicy
{
    public static void EnsureTypeRequest(string dictCode, string dictName)
    {
        if (string.IsNullOrWhiteSpace(dictCode))
        {
            throw new ValidationException("字典编码不能为空");
        }

        if (string.IsNullOrWhiteSpace(dictName))
        {
            throw new ValidationException("字典名称不能为空");
        }
    }

    public static void EnsureItemRequest(string itemLabel, string itemValue)
    {
        if (string.IsNullOrWhiteSpace(itemLabel))
        {
            throw new ValidationException("字典标签不能为空");
        }

        if (string.IsNullOrWhiteSpace(itemValue))
        {
            throw new ValidationException("字典值不能为空");
        }
    }
}
