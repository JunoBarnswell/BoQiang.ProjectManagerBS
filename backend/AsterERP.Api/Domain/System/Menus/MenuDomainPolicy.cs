using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Domain.System.Menus;

public static class MenuDomainPolicy
{
    public static void EnsureUpsertRequest(string menuName, string menuCode, string menuType)
    {
        if (string.IsNullOrWhiteSpace(menuName))
        {
            throw new ValidationException("菜单名称不能为空");
        }

        if (string.IsNullOrWhiteSpace(menuCode))
        {
            throw new ValidationException("菜单编码不能为空");
        }

        if (string.IsNullOrWhiteSpace(menuType))
        {
            throw new ValidationException("菜单类型不能为空");
        }
    }

    public static void EnsureNotSelfParent(string? parentCode, string currentCode)
    {
        if (!string.IsNullOrWhiteSpace(parentCode) && string.Equals(parentCode.Trim(), currentCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("菜单不能选择自身作为上级菜单");
        }
    }

    public static void EnsureDeletable(bool hasChildren)
    {
        if (hasChildren)
        {
            throw new ValidationException("存在下级菜单，不能删除", ErrorCodes.StateChangeNotAllowed);
        }
    }
}
