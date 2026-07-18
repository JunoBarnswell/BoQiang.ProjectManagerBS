import { lazy, type ReactNode } from 'react';
import { type RouteObject } from 'react-router-dom';

import { projectManagementPlatformRoutePrefix, projectManagementRoutePaths } from '../../../features/project-management/state/projectManagementPlatformRoutes';
import { routeMeta } from '../../navigation/routeMeta';
import { PermissionRoute } from '../PermissionRoute';

import { lazyPage } from './routeElements';

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

export const projectManagementPlatformRoutes: RouteObject[] = [
  platformProjectManagementRoute('', <ProjectManagementPage />),
  ...projectManagementRoutePaths.map((path) => platformProjectManagementRoute(path, projectManagementPageFor(path))),
];

function platformProjectManagementRoute(path: string, page: ReactNode): RouteObject {
  const permissionCode = path === 'project-audit-center'
    ? 'project-management:audit:view'
    : path === 'project-sync'
      ? ['project-management:sync:export', 'project-management:sync:import']
      : path === 'projects/:projectId/reports'
        ? 'project-management:report:export'
        : path === 'projects/:projectId/milestones'
          ? 'project-management:milestone:view'
          : path === 'projects/:projectId/members'
            ? 'project-management:member:view'
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
