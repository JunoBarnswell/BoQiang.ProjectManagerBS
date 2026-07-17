import { Navigate, Outlet, useParams } from 'react-router-dom';

import { useWorkspaceStore } from '../../core/state/workspaceStore';
import { Page403 } from '../../shared/status/Page403';

export function ApplicationWorkspaceRoute() {
  const { appCode, tenantId } = useParams();
  const normalizedTenantId = tenantId?.trim() ?? '';
  const normalizedAppCode = appCode?.trim().toUpperCase() ?? '';
  const currentWorkspace = useWorkspaceStore((state) => state.currentWorkspace);

  if (!normalizedTenantId || !normalizedAppCode || normalizedAppCode === 'SYSTEM') {
    return <Navigate replace to="/platform/applications" />;
  }

  if (
    currentWorkspace?.workspaceLevel === 'application' &&
    currentWorkspace.tenantId === normalizedTenantId &&
    currentWorkspace.appCode.toUpperCase() === normalizedAppCode
  ) {
    return <Outlet />;
  }

  return <Page403 />;
}
