import { applicationWorkspaceRoutes, workspaceRoutes } from '@/app/router/workspaceRoutes';

import { collectRouteMeta, findMatchingRouteMeta, type AppRouteMeta } from './routeMeta';

export type { AppRouteMeta };

const loginRouteMeta: AppRouteMeta = {
  breadcrumbKey: 'breadcrumbs.login',
  cachePolicy: 'none',
  iconKey: 'users',
  labelKey: 'nav.login',
  layoutVariant: 'login',
  path: '/login'
};

export const appRoutes: AppRouteMeta[] = [
  ...collectRouteMeta(applicationWorkspaceRoutes),
  ...collectRouteMeta(workspaceRoutes),
  loginRouteMeta
];

export function findRouteMeta(pathname: string): AppRouteMeta {
  return findMatchingRouteMeta(appRoutes, pathname);
}
