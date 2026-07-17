import { lazy, type ReactNode } from 'react';
import { Navigate, type RouteObject } from 'react-router-dom';

import { Page403 } from '../../shared/status/Page403';
import { Page404 } from '../../shared/status/Page404';
import { routeMeta, type AppRouteMeta } from '../navigation/routeMeta';

import { ApplicationDatabaseRequiredRoute } from './ApplicationDatabaseRequiredRoute';
import { PermissionRoute } from './PermissionRoute';
import { aiRoutes } from './routes/aiRoutes';
import { flowiseRoutes } from './routes/flowiseRoutes';
import { platformRoutes } from './routes/platformRoutes';
import { lazyPage } from './routes/routeElements';
import { systemRoutes } from './routes/systemRoutes';
import { workflowRoutes } from './routes/workflowRoutes';
import { RuntimePagePermissionRoute } from './RuntimePagePermissionRoute';

const DashboardPage = lazy(() => import('@/pages/dashboard/DashboardPage'));
const ModulesPage = lazy(() => import('../../pages/modules/ModulesPage').then((module) => ({ default: module.ModulesPage })));
const RuntimePage = lazy(() => import('../../pages/runtime/RuntimePage').then((module) => ({ default: module.RuntimePage })));
const ApplicationHomePage = lazy(() => import('../../pages/application-console/ApplicationHomePage').then((module) => ({ default: module.ApplicationHomePage })));
const ApplicationConsolePage = lazy(() => import('../../pages/application-console/ApplicationConsolePage').then((module) => ({ default: module.ApplicationConsolePage })));
const DevelopmentCenterHomePage = lazy(() => import('../../pages/application-console/DevelopmentCenterHomePage').then((module) => ({ default: module.DevelopmentCenterHomePage })));
const DevelopmentPagesPage = lazy(() => import('../../pages/application-console/development-center/ApplicationDevelopmentPagesPage').then((module) => ({ default: module.ApplicationDevelopmentPagesPage })));
const DesignerRoutePage = lazy(() => import('../../pages/application-console/development-center/DesignerRoutePage').then((module) => ({ default: module.DesignerRoutePage })));
const DataCenterHomePage = lazy(() => import('../../pages/application-console/DataCenterHomePage').then((module) => ({ default: module.DataCenterHomePage })));
const DataSourcesPage = lazy(() => import('../../pages/application-console/data-center/DataSourcesPage').then((module) => ({ default: module.DataSourcesPage })));
const DataSourceWorkbenchPage = lazy(() => import('../../pages/application-console/data-center/workbench/DataSourceWorkbenchPage').then((module) => ({ default: module.DataSourceWorkbenchPage })));
const ApplicationSystemAssignmentsPage = lazy(() => import('../../pages/application-console/data-center/ApplicationSystemAssignmentsPage').then((module) => ({ default: module.ApplicationSystemAssignmentsPage })));
const ConnectionTestsPage = lazy(() => import('../../pages/application-console/data-center/ConnectionTestsPage').then((module) => ({ default: module.ConnectionTestsPage })));
const DataModelsPage = lazy(() => import('../../pages/application-console/data-center/DataModelsPage').then((module) => ({ default: module.DataModelsPage })));
const ApiServicesPage = lazy(() => import('../../pages/application-console/data-center/ApiServicesPage').then((module) => ({ default: module.ApiServicesPage })));
const MicroflowsPage = lazy(() => import('../../pages/application-console/data-center/MicroflowsPage').then((module) => ({ default: module.MicroflowsPage })));
const EntitiesFieldsPage = lazy(() => import('../../pages/application-console/data-center/EntitiesFieldsPage').then((module) => ({ default: module.EntitiesFieldsPage })));
const DictionariesCodesPage = lazy(() => import('../../pages/application-console/data-center/DictionariesCodesPage').then((module) => ({ default: module.DictionariesCodesPage })));
const QueryDatasetsPage = lazy(() => import('../../pages/application-console/data-center/QueryDatasetsPage').then((module) => ({ default: module.QueryDatasetsPage })));
const IntegrationTasksPage = lazy(() => import('../../pages/application-console/data-center/IntegrationTasksPage').then((module) => ({ default: module.IntegrationTasksPage })));
const SettingsPage = lazy(() => import('../../pages/settings/SettingsPage').then((module) => ({ default: module.SettingsPage })));
const AsterSceneDashboardPage = lazy(() => import('../../features/aster-scene/pages/AsterSceneDashboardPage').then((module) => ({ default: module.AsterSceneDashboardPage })));
const AsterSceneAssetsPage = lazy(() => import('../../features/aster-scene/pages/AsterSceneAssetsPage').then((module) => ({ default: module.AsterSceneAssetsPage })));
const AsterSceneAdminPage = lazy(() => import('../../features/aster-scene/pages/AsterSceneAdminPage').then((module) => ({ default: module.AsterSceneAdminPage })));
const ImMessagesPage = lazy(() => import('../../pages/im/ImMessagesPage'));

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
      breadcrumbKey: 'breadcrumbs.infrastructure',
      cachePolicy: 'tab-alive',
      iconKey: 'dashboard',
      labelKey: 'nav.home',
      layoutVariant: 'app',
      path: '/home'
    }),
    element: lazyPage(<DashboardPage />)
  },
  {
    path: 'dashboard',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.asterSceneDashboard',
      cachePolicy: 'tab-alive',
      iconKey: 'cube',
      labelKey: 'nav.asterSceneDashboard',
      layoutVariant: 'app',
      path: '/dashboard'
    }),
    element: (
      <PermissionRoute permissionCode="asterscene:project:list">
        {lazyPage(<AsterSceneDashboardPage />)}
      </PermissionRoute>
    )
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
    element: lazyPage(<RuntimePage />)
  },
  {
    path: 'infrastructure',
    element: <Navigate replace to="/home" />
  },
  {
    path: 'modules',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.modules',
      cachePolicy: 'tab-alive',
      iconKey: 'module',
      labelKey: 'nav.modules',
      layoutVariant: 'app',
      path: '/modules'
    }),
    element: lazyPage(<ModulesPage />)
  },
  ...platformRoutes,
  ...systemRoutes,
  ...workflowRoutes,
  ...aiRoutes,
  ...flowiseRoutes,
  {
    path: 'im/messages',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.imMessages',
      cachePolicy: 'tab-alive',
      iconKey: 'mail',
      labelKey: 'nav.imMessages',
      layoutVariant: 'app',
      path: '/im/messages'
    }),
    element: lazyPage(<ImMessagesPage />)
  },
  {
    path: 'assets',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.asterSceneAssets',
      cachePolicy: 'tab-alive',
      iconKey: 'package',
      labelKey: 'nav.asterSceneAssets',
      layoutVariant: 'app',
      path: '/assets'
    }),
    element: (
      <PermissionRoute permissionCode="asterscene:asset:view">
        {lazyPage(<AsterSceneAssetsPage />)}
      </PermissionRoute>
    )
  },
  {
    path: 'admin/asterscene',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.asterSceneAdmin',
      cachePolicy: 'tab-alive',
      iconKey: 'shield',
      labelKey: 'nav.asterSceneAdmin',
      layoutVariant: 'app',
      path: '/admin/asterscene'
    }),
    element: (
      <PermissionRoute permissionCode="asterscene:admin:view">
        {lazyPage(<AsterSceneAdminPage />)}
      </PermissionRoute>
    )
  },
  {
    path: 'settings',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.settings',
      cachePolicy: 'tab-alive',
      iconKey: 'settings',
      labelKey: 'nav.settings',
      layoutVariant: 'app',
      path: '/settings'
    }),
    element: lazyPage(<SettingsPage />)
  },
  {
    path: '403',
    element: <Page403 />
  },
  {
    path: '*',
    element: <Page404 />
  }
];

const applicationAdminRoutePrefix = '/tenants/:tenantId/apps/:appCode/admin';
const applicationShellRoutePrefixes = [
  'admin/asterscene',
  'ai/',
  'assets',
  'flowise/',
  'im/messages',
  'system/',
  'workflows/'
];

const fixedApplicationWorkspaceRoutes: RouteObject[] = [
  {
    index: true,
    element: <Navigate replace to="home" />
  },
  applicationConsoleRoute('home', 'breadcrumbs.home', 'nav.home', 'home', 'app:home:view', <ApplicationHomePage />),
  applicationConsoleRoute('console', 'breadcrumbs.applicationConsole', 'nav.applicationConsole', 'module', 'app:console:view', <ApplicationConsolePage />),
  {
    path: 'pages/:pageCode',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.runtime',
      cachePolicy: 'tab-alive',
      iconKey: 'module',
      labelKey: 'nav.runtime',
      layoutVariant: 'app',
      path: `${applicationAdminRoutePrefix}/pages/:pageCode`
    }),
    element: (
      <ApplicationDatabaseRequiredRoute>
        <RuntimePagePermissionRoute>{lazyPage(<RuntimePage />)}</RuntimePagePermissionRoute>
      </ApplicationDatabaseRequiredRoute>
    )
  },
  applicationConsoleRoute('development-center', 'breadcrumbs.developmentCenter', 'nav.developmentCenter', 'code', 'app:development-center:view', <DevelopmentCenterHomePage />),
  applicationConsoleRoute('development-center/pages', 'breadcrumbs.developmentCenter', '业务对象页面工作区', 'files', 'app:development-center:business-object:view', <DevelopmentPagesPage />),
  applicationConsoleRoute('development-center/pages/:pageId/designer', 'breadcrumbs.developmentCenter', '低代码页面设计器', 'files', 'app:development-center:designer:view', <DesignerRoutePage />, 'detail'),
  applicationConsoleRoute('data-center', 'breadcrumbs.dataCenter', 'nav.dataCenter', 'database', 'app:data-center:view', <DataCenterHomePage />),
  applicationConsoleRoute('data-center/data-sources', 'breadcrumbs.dataCenter', '数据源管理', 'database', 'app:data-center:data-source:view', <DataSourcesPage />),
  applicationConsoleRoute('data-center/data-sources/:dataSourceId/workbench', 'breadcrumbs.dataCenter', '数据库工作台', 'database', 'app:data-center:data-source:view', <DataSourceWorkbenchPage />),
  applicationConsoleRoute('data-center/application-assignments', 'breadcrumbs.dataCenter', '应用系统分配', 'shield', 'app:data-center:data-source:view', <ApplicationSystemAssignmentsPage />),
  applicationConsoleRoute('data-center/connection-tests', 'breadcrumbs.dataCenter', '连接检测', 'shield', 'app:data-center:connection-test:view', <ConnectionTestsPage />),
  applicationConsoleRoute('data-center/models', 'breadcrumbs.dataCenter', '数据模型', 'table', 'app:data-center:data-model:view', <DataModelsPage />),
  applicationConsoleRoute('data-center/api-services', 'breadcrumbs.dataCenter', 'API 服务', 'api', 'app:data-center:api-service:view', <ApiServicesPage />),
  applicationConsoleRoute('data-center/microflows', 'breadcrumbs.dataCenter', '微流管理', 'module', 'app:data-center:microflow:view', <MicroflowsPage />),
  applicationConsoleRoute('data-center/entities-fields', 'breadcrumbs.dataCenter', '实体与字段', 'table', 'app:data-center:entity-field:view', <EntitiesFieldsPage />),
  applicationConsoleRoute('data-center/dictionaries-codes', 'breadcrumbs.dataCenter', '字典与编码', 'book', 'app:data-center:dictionary-code:view', <DictionariesCodesPage />),
  applicationConsoleRoute('data-center/query-datasets', 'breadcrumbs.dataCenter', '查询视图与数据集', 'activity', 'app:data-center:query-dataset:view', <QueryDatasetsPage />),
  applicationConsoleRoute('data-center/integration-tasks', 'breadcrumbs.dataCenter', '数据同步与集成任务', 'refresh', 'app:data-center:integration-task:view', <IntegrationTasksPage />)
];

function applicationConsoleRoute(
  path: string,
  breadcrumbKey: string,
  labelKey: string,
  iconKey: string,
  permissionCode: string,
  children: ReactNode,
  tabMode: 'detail' | 'menu' = 'menu',
  requiresApplicationDatabase = path !== 'home' && path !== 'console'
): RouteObject {
  const routeElement = (
    <PermissionRoute permissionCode={permissionCode}>
      {lazyPage(children)}
    </PermissionRoute>
  );

  return {
    path,
    handle: routeMeta({
      breadcrumbKey,
      cachePolicy: 'tab-alive',
      iconKey,
      labelKey,
      layoutVariant: 'app',
      path: `${applicationAdminRoutePrefix}/${path}`,
      tabMode
    }),
    element: requiresApplicationDatabase
      ? <ApplicationDatabaseRequiredRoute>{routeElement}</ApplicationDatabaseRequiredRoute>
      : routeElement
  };
}

function isApplicationShellWorkspaceRoute(route: RouteObject): route is RouteObject & { path: string } {
  const routePath = route.path;
  if (!routePath) {
    return false;
  }

  return applicationShellRoutePrefixes.some((prefix) => routePath === prefix || routePath.startsWith(prefix));
}

function toApplicationWorkspaceRoute(route: RouteObject): RouteObject {
  return {
    ...route,
    element: route.element ? <ApplicationDatabaseRequiredRoute>{route.element}</ApplicationDatabaseRequiredRoute> : route.element,
    handle: toApplicationWorkspaceRouteHandle(route.handle),
    children: route.children?.map(toApplicationWorkspaceRoute)
  } as RouteObject;
}

function toApplicationWorkspaceRouteHandle(handle: RouteObject['handle']): RouteObject['handle'] {
  const typedHandle = handle as { routeMeta?: AppRouteMeta } | undefined;
  if (!typedHandle?.routeMeta) {
    return handle;
  }

  return {
    ...typedHandle,
    routeMeta: {
      ...typedHandle.routeMeta,
      path: `${applicationAdminRoutePrefix}${typedHandle.routeMeta.path}`
    }
  };
}

const applicationShellWorkspaceRoutes = workspaceRoutes
  .filter(isApplicationShellWorkspaceRoute)
  .map(toApplicationWorkspaceRoute);

export const applicationWorkspaceRoutes: RouteObject[] = [
  ...fixedApplicationWorkspaceRoutes,
  ...applicationShellWorkspaceRoutes
];
