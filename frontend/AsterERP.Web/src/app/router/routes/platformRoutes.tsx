import { lazy, type ReactNode } from 'react';
import { type RouteObject } from 'react-router-dom';

import { routeMeta } from '../../navigation/routeMeta';
import { projectManagementPlatformRoutePrefix, projectManagementRoutePaths } from '../../../features/project-management/state/projectManagementPlatformRoutes';
import { PermissionRoute } from '../PermissionRoute';
import { TenantAdminRoute } from '../TenantAdminRoute';

import { lazyPage } from './routeElements';

const PlatformTenantsPage = lazy(() => import('../../../pages/platform/PlatformTenantsPage').then((module) => ({ default: module.PlatformTenantsPage })));
const PlatformApplicationsPage = lazy(() => import('../../../pages/platform/PlatformApplicationsPage').then((module) => ({ default: module.PlatformApplicationsPage })));
const PlatformTenantAppsPage = lazy(() => import('../../../pages/platform/PlatformTenantAppsPage').then((module) => ({ default: module.PlatformTenantAppsPage })));
const PlatformUserTenantsPage = lazy(() => import('../../../pages/platform/PlatformUserTenantsPage').then((module) => ({ default: module.PlatformUserTenantsPage })));
const PlatformUserAppRolesPage = lazy(() => import('../../../pages/platform/PlatformUserAppRolesPage').then((module) => ({ default: module.PlatformUserAppRolesPage })));
const TenantAppsPage = lazy(() => import('../../../pages/tenant/TenantAppsPage').then((module) => ({ default: module.TenantAppsPage })));
const ProjectManagementPage = lazy(() => import('../../../pages/project-management/ProjectManagementPage').then((module) => ({ default: module.ProjectManagementPage })));
const ProjectManagementTaskWorkspacePage = lazy(() => import('../../../pages/project-management/ProjectManagementTaskWorkspacePage').then((module) => ({ default: module.ProjectManagementTaskWorkspacePage })));
const ProjectManagementDataSpacePage = lazy(() => import('../../../pages/project-management/ProjectManagementDataSpacePage').then((module) => ({ default: module.ProjectManagementDataSpacePage })));
const ProjectManagementAuditPage = lazy(() => import('../../../pages/project-management/ProjectManagementAuditPage').then((module) => ({ default: module.ProjectManagementAuditPage })));
const ProjectManagementOverviewPage = lazy(() => import('../../../pages/project-management/ProjectManagementOverviewPage').then((module) => ({ default: module.ProjectManagementOverviewPage })));
const ProjectManagementMembersPage = lazy(() => import('../../../pages/project-management/ProjectManagementMembersPage').then((module) => ({ default: module.ProjectManagementMembersPage })));
const ProjectManagementMilestonesPage = lazy(() => import('../../../pages/project-management/ProjectManagementMilestonesPage').then((module) => ({ default: module.ProjectManagementMilestonesPage })));
const ProjectManagementMyWorkPage = lazy(() => import('../../../pages/project-management/ProjectManagementMyWorkPage').then((module) => ({ default: module.ProjectManagementMyWorkPage })));
const ProjectManagementRecycleBinPage = lazy(() => import('../../../pages/project-management/ProjectManagementRecycleBinPage').then((module) => ({ default: module.ProjectManagementRecycleBinPage })));
const ProjectManagementReportsPage = lazy(() => import('../../../pages/project-management/ProjectManagementReportsPage').then((module) => ({ default: module.ProjectManagementReportsPage })));
const ProjectManagementSearchPage = lazy(() => import('../../../pages/project-management/ProjectManagementSearchPage').then((module) => ({ default: module.ProjectManagementSearchPage })));
const ProjectManagementSyncPage = lazy(() => import('../../../pages/project-management/ProjectManagementSyncPage').then((module) => ({ default: module.ProjectManagementSyncPage })));

export const platformRoutes: RouteObject[] = [
  platformProjectManagementRoute('', <ProjectManagementPage />),
  ...projectManagementRoutePaths.map((path) => platformProjectManagementRoute(path, projectManagementPageFor(path))),
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

function platformProjectManagementRoute(path: string, page: ReactNode): RouteObject {
  const permissionCode = path === 'project-audit-center'
    ? 'project-management:audit:view'
    : path === 'project-sync'
      ? 'project-management:sync:export'
      : path === 'projects/:projectId/reports'
        ? 'project-management:report:export'
        : path === 'projects/:projectId/overview'
          ? 'project-management:project:view'
          : path.includes('projects/:projectId')
            ? 'project-management:task:view'
            : 'project-management:project:view';
  const routePath = path ? `${projectManagementPlatformRoutePrefix}/${path}` : projectManagementPlatformRoutePrefix;

  return {
    path: routePath.slice(1),
    handle: routeMeta({
      breadcrumbKey: path === 'my-work' ? 'breadcrumbs.projectManagementMyWork' : 'breadcrumbs.projectManagement',
      cachePolicy: 'tab-alive',
      iconKey: 'activity',
      labelKey: path === 'my-work' ? 'nav.projectManagementMyWork' : 'nav.projectManagement',
      layoutVariant: 'app',
      path: routePath,
    }),
    element: <PermissionRoute permissionCode={permissionCode}>{lazyPage(page)}</PermissionRoute>,
  };
}

function projectManagementPageFor(path: typeof projectManagementRoutePaths[number]): ReactNode {
  if (path === 'project-data-space') return <ProjectManagementDataSpacePage />;
  if (path === 'project-sync') return <ProjectManagementSyncPage />;
  if (path === 'project-search') return <ProjectManagementSearchPage />;
  if (path === 'project-audit-center') return <ProjectManagementAuditPage />;
  if (path === 'my-work') return <ProjectManagementMyWorkPage />;
  if (path === 'project-recycle-bin') return <ProjectManagementRecycleBinPage />;
  if (path === 'projects/:projectId/overview') return <ProjectManagementOverviewPage />;
  if (path === 'projects/:projectId/milestones') return <ProjectManagementMilestonesPage />;
  if (path === 'projects/:projectId/members') return <ProjectManagementMembersPage />;
  if (path === 'projects/:projectId/reports') return <ProjectManagementReportsPage />;
  if (path.includes('projects/:projectId')) return <ProjectManagementTaskWorkspacePage />;
  return <ProjectManagementPage />;
}
