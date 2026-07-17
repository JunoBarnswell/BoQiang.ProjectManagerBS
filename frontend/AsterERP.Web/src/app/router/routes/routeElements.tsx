import { Suspense, type ReactNode } from 'react';
import { Navigate, useParams } from 'react-router-dom';

import { PageLoading } from '../../../shared/status/PageLoading';

export function lazyPage(children: ReactNode) {
  return <Suspense fallback={<PageLoading />}>{children}</Suspense>;
}

export function ParamRedirect({ paramName = 'resourceId', to }: { paramName?: string; to: string }) {
  const params = useParams();
  const value = params[paramName];
  return <Navigate replace to={value ? `${to}/${value}` : to} />;
}
