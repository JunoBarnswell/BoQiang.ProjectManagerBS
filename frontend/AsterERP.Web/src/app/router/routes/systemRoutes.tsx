import { lazy, type ReactNode } from 'react';
import { type RouteObject } from 'react-router-dom';

import { routeMeta } from '../../navigation/routeMeta';
import { PermissionRoute } from '../PermissionRoute';

import { lazyPage } from './routeElements';

const DictsPage = lazy(() => import('../../../pages/system/dicts/DictsPage').then((module) => ({ default: module.DictsPage })));
const MenusPage = lazy(() => import('../../../pages/system/menus/MenusPage').then((module) => ({ default: module.MenusPage })));
const DepartmentsPage = lazy(() => import('../../../pages/system/departments/DepartmentsPage').then((module) => ({ default: module.DepartmentsPage })));
const PositionsPage = lazy(() => import('../../../pages/system/positions/PositionsPage').then((module) => ({ default: module.PositionsPage })));
const RolesPage = lazy(() => import('../../../pages/system/roles/RolesPage').then((module) => ({ default: module.RolesPage })));
const UsersPage = lazy(() => import('../../../pages/system/users/UsersPage').then((module) => ({ default: module.UsersPage })));
const ParametersPage = lazy(() => import('../../../pages/system/parameters/ParametersPage').then((module) => ({ default: module.ParametersPage })));
const AbpInfrastructureSettingsPage = lazy(() => import('../../../pages/system/abp-infrastructure-settings/AbpInfrastructureSettingsPage').then((module) => ({ default: module.AbpInfrastructureSettingsPage })));
const AnnouncementsPage = lazy(() => import('../../../pages/system/announcements/AnnouncementsPage').then((module) => ({ default: module.AnnouncementsPage })));
const OperationLogsPage = lazy(() => import('../../../pages/system/operation-logs/OperationLogsPage').then((module) => ({ default: module.OperationLogsPage })));
const LoginLogsPage = lazy(() => import('../../../pages/system/login-logs/LoginLogsPage').then((module) => ({ default: module.LoginLogsPage })));
const OnlineUsersPage = lazy(() => import('../../../pages/system/online-users/OnlineUsersPage').then((module) => ({ default: module.OnlineUsersPage })));
const ScheduledJobsPage = lazy(() => import('../../../pages/system/scheduled-jobs/ScheduledJobsPage').then((module) => ({ default: module.ScheduledJobsPage })));
const FilesPage = lazy(() => import('../../../pages/system/files/FilesPage').then((module) => ({ default: module.FilesPage })));
const PrintCenterPage = lazy(() => import('../../../features/print-center/pages/PrintCenterPage').then((module) => ({ default: module.PrintCenterPage })));
const PrintTemplateDesignerPage = lazy(() => import('../../../features/print-center/pages/PrintTemplateDesignerPage').then((module) => ({ default: module.PrintTemplateDesignerPage })));

type WorkspaceRouteCachePolicy = 'none' | 'tab-alive';

function systemRoute(
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

export const systemRoutes: RouteObject[] = [
  systemRoute('system/dicts', 'breadcrumbs.systemDicts', 'nav.systemDicts', 'moduleBox', 'system:dict:query', <DictsPage />),
  systemRoute('system/users', 'breadcrumbs.systemUsers', 'nav.systemUsers', 'users', 'system:user:query', <UsersPage />),
  systemRoute('system/departments', 'breadcrumbs.systemDepartments', 'nav.systemDepartments', 'system', 'system:dept:query', <DepartmentsPage />),
  systemRoute('system/positions', 'breadcrumbs.systemPositions', 'nav.systemPositions', 'usersManagement', 'system:position:query', <PositionsPage />),
  systemRoute('system/roles', 'breadcrumbs.systemRoles', 'nav.systemRoles', 'roles', 'system:role:query', <RolesPage />),
  systemRoute('system/menus', 'breadcrumbs.systemMenus', 'nav.systemMenus', 'menu', 'system:menu:query', <MenusPage />),
  systemRoute('system/parameters', 'breadcrumbs.systemParameters', 'nav.systemParameters', 'settings', 'system:parameter:query', <ParametersPage />),
  systemRoute(
    'system/abp-infrastructure-settings',
    'breadcrumbs.abpInfrastructureSettings',
    'nav.abpInfrastructureSettings',
    'activity',
    'system:abp-setting:query',
    <AbpInfrastructureSettingsPage />
  ),
  systemRoute('system/announcements', 'breadcrumbs.systemAnnouncements', 'nav.systemAnnouncements', 'settings', 'system:announcement:query', <AnnouncementsPage />),
  systemRoute('system/operation-logs', 'breadcrumbs.systemOperationLogs', 'nav.systemOperationLogs', 'module', 'system:operation-log:query', <OperationLogsPage />),
  systemRoute('system/login-logs', 'breadcrumbs.systemLoginLogs', 'nav.systemLoginLogs', 'users', 'system:login-log:query', <LoginLogsPage />),
  systemRoute('system/online-users', 'breadcrumbs.systemOnlineUsers', 'nav.systemOnlineUsers', 'usersManagement', 'system:online-user:query', <OnlineUsersPage />),
  systemRoute('system/scheduled-jobs', 'breadcrumbs.systemScheduledJobs', 'nav.systemScheduledJobs', 'activity', 'system:scheduled-job:query', <ScheduledJobsPage />),
  systemRoute('system/files', 'breadcrumbs.systemFiles', 'nav.systemFiles', 'fileSearch', 'system:file:query', <FilesPage />),
  systemRoute('system/print-center', 'breadcrumbs.systemPrintCenter', 'nav.systemPrintCenter', 'printer', 'system:print:query', <PrintCenterPage />),
  systemRoute(
    'system/print-center/new',
    'breadcrumbs.systemPrintCenter',
    'nav.systemPrintCenter',
    'printer',
    'system:print:edit',
    <PrintTemplateDesignerPage />,
    'none',
    'detail'
  ),
  systemRoute(
    'system/print-center/:templateId/designer',
    'breadcrumbs.systemPrintCenter',
    'nav.systemPrintCenter',
    'printer',
    'system:print:edit',
    <PrintTemplateDesignerPage />,
    'none',
    'detail'
  )
];
