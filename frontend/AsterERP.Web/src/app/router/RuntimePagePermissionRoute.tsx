import type { ReactNode } from 'react';
import { useParams } from 'react-router-dom';

import { usePermissionStore } from '../../core/state';
import { Page403 } from '../../shared/status/Page403';

interface RuntimePagePermissionRouteProps {
  children: ReactNode;
}

export function buildRuntimePageViewPermission(pageCode: string | undefined): string {
  const normalizedPageCode = pageCode?.trim().toLowerCase().replaceAll('_', '-') || 'unknown';
  return `app:runtime-page:${normalizedPageCode}:view`;
}

export function RuntimePagePermissionRoute({ children }: RuntimePagePermissionRouteProps) {
  const { pageCode } = useParams<{ pageCode: string }>();
  const hasPermission = usePermissionStore((state) => state.hasPermission);

  if (!hasPermission(buildRuntimePageViewPermission(pageCode))) {
    return <Page403 />;
  }

  return children;
}
