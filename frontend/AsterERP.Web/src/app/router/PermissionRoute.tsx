import type { ReactNode } from 'react';

import { usePermissionStore } from '../../core/state';
import { Page403 } from '../../shared/status/Page403';

interface PermissionRouteProps {
  children: ReactNode;
  permissionCode?: string | string[];
}

export function PermissionRoute({ children, permissionCode }: PermissionRouteProps) {
  const hasPermission = usePermissionStore((state) => state.hasPermission);

  if (!permissionCode) {
    return children;
  }

  if (!hasPermission(permissionCode)) {
    return <Page403 />;
  }

  return children;
}
