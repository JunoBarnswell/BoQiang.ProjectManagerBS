import { lazy } from 'react';
import { type RouteObject } from 'react-router-dom';

import { routeMeta } from '../../navigation/routeMeta';
import { PermissionRoute } from '../PermissionRoute';
import { TenantAdminRoute } from '../TenantAdminRoute';

import { lazyPage } from './routeElements';

const PlatformTenantsPage = lazy(() => import('../../../pages/platform/PlatformTenantsPage').then((module) => ({ default: module.PlatformTenantsPage })));
const PlatformApplicationsPage = lazy(() => import('../../../pages/platform/PlatformApplicationsPage').then((module) => ({ default: module.PlatformApplicationsPage })));
const PlatformTenantAppsPage = lazy(() => import('../../../pages/platform/PlatformTenantAppsPage').then((module) => ({ default: module.PlatformTenantAppsPage })));
const PlatformUserTenantsPage = lazy(() => import('../../../pages/platform/PlatformUserTenantsPage').then((module) => ({ default: module.PlatformUserTenantsPage })));
const PlatformUserAppRolesPage = lazy(() => import('../../../pages/platform/PlatformUserAppRolesPage').then((module) => ({ default: module.PlatformUserAppRolesPage })));
const TenantAppsPage = lazy(() => import('../../../pages/tenant/TenantAppsPage').then((module) => ({ default: module.TenantAppsPage })));

export const platformRoutes: RouteObject[] = [
  {
    path: 'platform/tenants',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.platformTenants',
      cachePolicy: 'tab-alive',
      iconKey: 'system',
      labelKey: 'nav.platformTenants',
      layoutVariant: 'app',
      path: '/platform/tenants'
    }),
    element: (
      <PermissionRoute permissionCode="platform:tenant:query">
        {lazyPage(<PlatformTenantsPage />)}
      </PermissionRoute>
    )
  },
  {
    path: 'platform/applications',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.platformApplications',
      cachePolicy: 'tab-alive',
      iconKey: 'module',
      labelKey: 'nav.platformApplications',
      layoutVariant: 'app',
      path: '/platform/applications'
    }),
    element: (
      <PermissionRoute permissionCode="platform:application:query">
        {lazyPage(<PlatformApplicationsPage />)}
      </PermissionRoute>
    )
  },
  {
    path: 'platform/tenant-apps',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.platformTenantApps',
      cachePolicy: 'tab-alive',
      iconKey: 'moduleBox',
      labelKey: 'nav.platformTenantApps',
      layoutVariant: 'app',
      path: '/platform/tenant-apps'
    }),
    element: (
      <PermissionRoute permissionCode="platform:tenant-app:query">
        {lazyPage(<PlatformTenantAppsPage />)}
      </PermissionRoute>
    )
  },
  {
    path: 'platform/user-tenants',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.platformUserTenants',
      cachePolicy: 'tab-alive',
      iconKey: 'users',
      labelKey: 'nav.platformUserTenants',
      layoutVariant: 'app',
      path: '/platform/user-tenants'
    }),
    element: (
      <PermissionRoute permissionCode="platform:user-tenant:query">
        {lazyPage(<PlatformUserTenantsPage />)}
      </PermissionRoute>
    )
  },
  {
    path: 'platform/user-app-roles',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.platformUserAppRoles',
      cachePolicy: 'tab-alive',
      iconKey: 'roles',
      labelKey: 'nav.platformUserAppRoles',
      layoutVariant: 'app',
      path: '/platform/user-app-roles'
    }),
    element: (
      <PermissionRoute permissionCode="platform:user-app-role:query">
        {lazyPage(<PlatformUserAppRolesPage />)}
      </PermissionRoute>
    )
  },
  {
    path: 'tenant/apps',
    element: (
      <TenantAdminRoute>
        {lazyPage(<TenantAppsPage />)}
      </TenantAdminRoute>
    )
  }
];
