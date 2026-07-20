import { lazy, type ReactNode } from 'react';
import { type RouteObject } from 'react-router-dom';

import { projectManagementPlatformRoutePrefix, projectManagementRoutePaths } from '../../../features/project-management/state/projectManagementPlatformRoutes';
import { routeMeta } from '../../navigation/routeMeta';
import { PermissionRoute } from '../PermissionRoute';

import { lazyPage } from './routeElements';

const ProjectManagementPage = lazy(() => import('../../../pages/project-management/ProjectManagementPage').then((module) => ({ default: module.ProjectManagementPage })));
const ProjectManagementOverviewPage = lazy(() => import('../../../pages/project-management/ProjectManagementOverviewPage').then((module) => ({ default: module.ProjectManagementOverviewPage })));
const ProjectManagementRequirementsPage = lazy(() => import('../../../pages/project-management/ProjectManagementRequirementsPage').then((module) => ({ default: module.ProjectManagementRequirementsPage })));

export const projectManagementPlatformRoutes: RouteObject[] = [
  platformProjectManagementRoute('', <ProjectManagementPage />),
  ...projectManagementRoutePaths.map((path) => platformProjectManagementRoute(path, projectManagementPageFor(path))),
];

function platformProjectManagementRoute(path: string, page: ReactNode): RouteObject {
  const permissionCode = path === 'projects/:projectId/overview' ? 'project-management:project:view' : 'project-management:task:view';
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
  if (path === 'projects/:projectId/overview') return <ProjectManagementOverviewPage />;
  return <ProjectManagementRequirementsPage />;
}
