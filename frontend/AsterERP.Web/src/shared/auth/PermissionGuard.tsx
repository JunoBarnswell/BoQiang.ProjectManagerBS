import type { ReactNode } from 'react';

import { usePermission } from '../../core/auth/usePermission';
import { Page403 } from '../status/Page403';


interface PermissionGuardProps {
  children: ReactNode;
  fallback?: ReactNode;
  code: string | string[];
}

export function PermissionGuard({ children, code, fallback = <Page403 /> }: PermissionGuardProps) {
  const { hasPermission } = usePermission(code);

  if (!hasPermission) {
    return <>{fallback}</>;
  }

  return <>{children}</>;
}
