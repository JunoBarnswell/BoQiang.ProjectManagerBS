import { usePermissionStore } from '../state';

export function usePermission(permissionCode?: string | string[]) {
  const permissionCodes = usePermissionStore((state) => state.permissionCodes);
  const hasPermission = usePermissionStore((state) => state.hasPermission);
  const isAllowed = hasPermission(permissionCode);

  return {
    hasPermission: isAllowed,
    permissionCodes
  };
}
