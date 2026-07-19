import { lazy, Suspense, type ReactNode } from 'react';
import { Navigate, type RouteObject } from 'react-router-dom';

import { projectManagementPlatformRoutePrefix, projectManagementRoutePaths } from '../../features/project-management/state/projectManagementPlatformRoutes';
import { Page403 } from '../../shared/status/Page403';
import { Page404 } from '../../shared/status/Page404';
import { PageLoading } from '../../shared/status/PageLoading';
import { routeMeta } from '../navigation/routeMeta';

import { projectManagementPlatformRoutes } from './routes/projectManagementPlatformRoutes';
import { RuntimePagePermissionRoute } from './RuntimePagePermissionRoute';

const DashboardPage = lazy(() => import('@/pages/dashboard/DashboardPage'));
const RuntimePage = lazy(() => import('../../pages/runtime/RuntimePage').then((module) => ({ default: module.RuntimePage })));
const legacyProjectManagementRoutes: RouteObject[] = projectManagementRoutePaths.map((path) => ({
  path,
  element: <Navigate replace to={projectManagementPlatformRoutePrefix} />,
}));

function lazyPage(children: ReactNode) {
  return <Suspense fallback={<PageLoading />}>{children}</Suspense>;
}

export const workspaceRoutes: RouteObject[] = [
  {
    index: true,
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.home',
      cachePolicy: 'none',
      iconKey: 'home',
      labelKey: 'nav.home',
      layoutVariant: 'app',
      path: '/'
    }),
    element: <Navigate replace to="/home" />
  },
  {
    path: 'home',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.dashboard',
      cachePolicy: 'tab-alive',
      iconKey: 'dashboard',
      labelKey: 'nav.dashboard',
      layoutVariant: 'app',
      path: '/home'
    }),
    element: lazyPage(<DashboardPage />)
  },
  {
    path: 'dashboard',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.dashboard',
      cachePolicy: 'tab-alive',
      iconKey: 'dashboard',
      labelKey: 'nav.dashboard',
      layoutVariant: 'app',
      path: '/dashboard'
    }),
    element: lazyPage(<DashboardPage />)
  },
  {
    path: 'pages/:pageCode',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.runtime',
      cachePolicy: 'tab-alive',
      iconKey: 'module',
      labelKey: 'nav.runtime',
      layoutVariant: 'app',
      path: '/pages/:pageCode'
    }),
    element: <RuntimePagePermissionRoute>{lazyPage(<RuntimePage />)}</RuntimePagePermissionRoute>
  },
  ...projectManagementPlatformRoutes,
  ...legacyProjectManagementRoutes,
  {
    path: '403',
    element: <Page403 />
  },
  {
    path: '*',
    element: <Page404 />
  }
];

export const applicationWorkspaceRoutes: RouteObject[] = workspaceRoutes;
