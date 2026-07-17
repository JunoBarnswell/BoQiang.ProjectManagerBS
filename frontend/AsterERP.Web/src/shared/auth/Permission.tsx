import type { ReactNode } from 'react';

import { PermissionGuard } from './PermissionGuard';

interface PermissionProps {
  children: ReactNode;
  code: string;
}

export function Permission({ children, code }: PermissionProps) {
  return <PermissionGuard code={code}>{children}</PermissionGuard>;
}
