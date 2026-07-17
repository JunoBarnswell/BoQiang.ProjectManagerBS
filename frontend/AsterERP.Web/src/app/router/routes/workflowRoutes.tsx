import { lazy, type ReactNode } from 'react';
import { type RouteObject } from 'react-router-dom';

import { routeMeta } from '../../navigation/routeMeta';
import { PermissionRoute } from '../PermissionRoute';

import { lazyPage } from './routeElements';

const WorkflowBindingsPage = lazy(() => import('../../../pages/workflows/WorkflowBindingsPage').then((module) => ({ default: module.WorkflowBindingsPage })));
const WorkflowCalendarsPage = lazy(() => import('../../../pages/workflows/WorkflowCalendarsPage').then((module) => ({ default: module.WorkflowCalendarsPage })));
const WorkflowCategoriesPage = lazy(() => import('../../../pages/workflows/WorkflowCategoriesPage').then((module) => ({ default: module.WorkflowCategoriesPage })));
const WorkflowDelegationsPage = lazy(() => import('../../../pages/workflows/WorkflowDelegationsPage').then((module) => ({ default: module.WorkflowDelegationsPage })));
const WorkflowDesignerPage = lazy(() => import('../../../pages/workflows/WorkflowDesignerPage').then((module) => ({ default: module.WorkflowDesignerPage })));
const WorkflowDraftsPage = lazy(() => import('../../../pages/workflows/WorkflowDraftsPage').then((module) => ({ default: module.WorkflowDraftsPage })));
const WorkflowFormsPage = lazy(() => import('../../../pages/workflows/WorkflowFormsPage').then((module) => ({ default: module.WorkflowFormsPage })));
const WorkflowHistoryPage = lazy(() => import('../../../pages/workflows/WorkflowHistoryPage').then((module) => ({ default: module.WorkflowHistoryPage })));
const WorkflowInitiatePage = lazy(() => import('../../../pages/workflows/WorkflowInitiatePage').then((module) => ({ default: module.WorkflowInitiatePage })));
const WorkflowInstancePage = lazy(() => import('../../../pages/workflows/WorkflowInstancePage').then((module) => ({ default: module.WorkflowInstancePage })));
const WorkflowMonitoringPage = lazy(() => import('../../../pages/workflows/WorkflowMonitoringPage').then((module) => ({ default: module.WorkflowMonitoringPage })));
const WorkflowModelsPage = lazy(() => import('../../../pages/workflows/WorkflowModelsPage').then((module) => ({ default: module.WorkflowModelsPage })));
const WorkflowNotificationsPage = lazy(() => import('../../../pages/workflows/WorkflowNotificationsPage').then((module) => ({ default: module.WorkflowNotificationsPage })));
const WorkflowReportsPage = lazy(() => import('../../../pages/workflows/WorkflowReportsPage').then((module) => ({ default: module.WorkflowReportsPage })));
const WorkflowTasksPage = lazy(() => import('../../../pages/workflows/WorkflowTasksPage').then((module) => ({ default: module.WorkflowTasksPage })));

type WorkspaceRouteCachePolicy = 'none' | 'tab-alive';

function workflowRoute(
  path: string,
  breadcrumbKey: string,
  labelKey: string,
  iconKey: string,
  permissionCode: string,
  children: ReactNode,
  cachePolicy: WorkspaceRouteCachePolicy = 'tab-alive',
  tabMode: 'detail' | 'menu' = 'menu'
): RouteObject {
  return {
    path,
    handle: routeMeta({
      breadcrumbKey,
      cachePolicy,
      iconKey,
      labelKey,
      layoutVariant: 'app',
      path: `/${path}`,
      tabMode
    }),
    element: (
      <PermissionRoute permissionCode={permissionCode}>
        {lazyPage(children)}
      </PermissionRoute>
    )
  };
}

export const workflowRoutes: RouteObject[] = [
  workflowRoute('workflows/initiate', 'breadcrumbs.workflowInitiate', 'nav.workflowInitiate', 'activity', 'workflow:instance:start', <WorkflowInitiatePage />),
  workflowRoute('workflows/forms', 'breadcrumbs.workflowForms', 'nav.workflowForms', 'menu', 'workflow:form:query', <WorkflowFormsPage />),
  workflowRoute('workflows/models', 'breadcrumbs.workflowModels', 'nav.workflowModels', 'activity', 'workflow:model:query', <WorkflowModelsPage />),
  workflowRoute('workflows/models/:modelId/designer', 'breadcrumbs.workflowDesigner', 'nav.workflowDesigner', 'wrench', 'workflow:model:edit', <WorkflowDesignerPage />, 'none', 'detail'),
  workflowRoute('workflows/bindings', 'breadcrumbs.workflowBindings', 'nav.workflowBindings', 'menu', 'workflow:binding:query', <WorkflowBindingsPage />),
  workflowRoute('workflows/categories', 'breadcrumbs.workflowCategories', 'nav.workflowCategories', 'menu', 'workflow:category:query', <WorkflowCategoriesPage />),
  workflowRoute('workflows/monitoring', 'breadcrumbs.workflowMonitoring', 'nav.workflowMonitoring', 'activity', 'workflow:instance:query', <WorkflowMonitoringPage />),
  workflowRoute('workflows/tasks', 'breadcrumbs.workflowTasks', 'nav.workflowTasks', 'list', 'workflow:task:query', <WorkflowTasksPage />),
  workflowRoute('workflows/drafts', 'breadcrumbs.workflowDrafts', 'nav.workflowDrafts', 'list', 'workflow:draft:query', <WorkflowDraftsPage />),
  workflowRoute('workflows/instances/:processInstanceId', 'breadcrumbs.workflowInstance', 'nav.workflowInstance', 'activity', 'workflow:instance:query', <WorkflowInstancePage />, 'none', 'detail'),
  workflowRoute('workflows/history', 'breadcrumbs.workflowHistory', 'nav.workflowHistory', 'activity', 'workflow:history:query', <WorkflowHistoryPage />),
  workflowRoute('workflows/reports', 'breadcrumbs.workflowReports', 'nav.workflowReports', 'dashboard', 'workflow:report:query', <WorkflowReportsPage />),
  workflowRoute('workflows/notifications', 'breadcrumbs.workflowNotifications', 'nav.workflowNotifications', 'activity', 'workflow:notification:task:query', <WorkflowNotificationsPage />),
  workflowRoute('workflows/delegations', 'breadcrumbs.workflowDelegations', 'nav.workflowDelegations', 'users', 'workflow:delegation:query', <WorkflowDelegationsPage />),
  workflowRoute('workflows/calendars', 'breadcrumbs.workflowCalendars', 'nav.workflowCalendars', 'activity', 'workflow:calendar:query', <WorkflowCalendarsPage />)
];
