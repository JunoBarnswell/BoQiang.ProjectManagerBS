import type { ReactNode } from 'react';

import { useAuthStore, usePermissionStore, useWorkspaceStore } from '../../core/state';
import { Page403 } from '../../shared/status/Page403';

interface TenantAdminRouteProps {
  children: ReactNode;
}

export function TenantAdminRoute({ children }: TenantAdminRouteProps) {
  const user = useAuthStore((state) => state.user);
  const currentWorkspace = useWorkspaceStore((state) => state.currentWorkspace);
  const hasPermission = usePermissionStore((state) => state.hasPermission);

  if (!currentWorkspace || (!user?.isTenantAdmin && !hasPermission('*'))) {
    return <Page403 />;
  }

  return children;
}
