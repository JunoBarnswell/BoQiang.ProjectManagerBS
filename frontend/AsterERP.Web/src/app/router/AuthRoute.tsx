import { Navigate, Outlet, useLocation } from 'react-router-dom';

import { useAuthStore, useWorkspaceStore } from '../../core/state';
import { PageLoading } from '../../shared/status/PageLoading';

export function AuthRoute() {
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const isLoading = useAuthStore((state) => state.isLoading);
  const currentWorkspace = useWorkspaceStore((state) => state.currentWorkspace);
  const location = useLocation();

  if (isLoading) {
    return <PageLoading />;
  }

  if (!isAuthenticated) {
    return <Navigate replace state={{ from: location }} to={resolveLoginPath(location.pathname)} />;
  }

  if (!currentWorkspace && location.pathname !== '/workspace') {
    return <Navigate replace to="/workspace" />;
  }

  return <Outlet />;
}

function resolveLoginPath(pathname: string): string {
  const match = /^\/tenants\/([^/]+)\/apps\/([^/]+)\/admin(?:\/|$)/i.exec(pathname);
  if (!match) {
    return '/login';
  }

  const tenantId = match[1] ?? '';
  const appCode = match[2] ?? '';
  return `/tenants/${encodeURIComponent(tenantId)}/apps/${encodeURIComponent(appCode.toUpperCase())}/login`;
}
