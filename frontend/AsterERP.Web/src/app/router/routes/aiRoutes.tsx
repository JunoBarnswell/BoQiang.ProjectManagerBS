import { lazy } from 'react';
import { Navigate, type RouteObject } from 'react-router-dom';

import { routeMeta } from '../../navigation/routeMeta';
import { PermissionRoute } from '../PermissionRoute';

import { lazyPage } from './routeElements';

const AiWorkbenchPage = lazy(() => import('../../../features/ai-center/AiWorkbenchPage').then((module) => ({ default: module.AiWorkbenchPage })));
const AiCapabilityCenterPage = lazy(() => import('../../../features/ai-center/AiCapabilityCenterPage').then((module) => ({ default: module.AiCapabilityCenterPage })));
const AiObservabilityPage = lazy(() => import('../../../features/ai-center/AiObservabilityPage').then((module) => ({ default: module.AiObservabilityPage })));
const AiSecurityPage = lazy(() => import('../../../features/ai-center/AiSecurityPage').then((module) => ({ default: module.AiSecurityPage })));
const AiSettingsPage = lazy(() => import('../../../features/ai-center/AiSettingsPage').then((module) => ({ default: module.AiSettingsPage })));

export const aiRoutes: RouteObject[] = [
  {
    path: 'ai/workbench',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.aiWorkbench',
      cachePolicy: 'tab-alive',
      iconKey: 'activity',
      labelKey: 'nav.aiWorkbench',
      layoutVariant: 'app',
      path: '/ai/workbench'
    }),
    element: (
      <PermissionRoute permissionCode="ai:workbench:view">
        {lazyPage(<AiWorkbenchPage />)}
      </PermissionRoute>
    )
  },
  {
    path: 'ai/capability',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.aiCapability',
      cachePolicy: 'tab-alive',
      iconKey: 'moduleBox',
      labelKey: 'nav.aiCapability',
      layoutVariant: 'app',
      path: '/ai/capability'
    }),
    element: (
      <PermissionRoute permissionCode="ai:capability:view">
        {lazyPage(<AiCapabilityCenterPage />)}
      </PermissionRoute>
    )
  },
  {
    path: 'ai/observability',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.aiObservability',
      cachePolicy: 'tab-alive',
      iconKey: 'dashboard',
      labelKey: 'nav.aiObservability',
      layoutVariant: 'app',
      path: '/ai/observability'
    }),
    element: (
      <PermissionRoute permissionCode="ai:observability:view">
        {lazyPage(<AiObservabilityPage />)}
      </PermissionRoute>
    )
  },
  {
    path: 'ai/security',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.aiSecurity',
      cachePolicy: 'tab-alive',
      iconKey: 'shield',
      labelKey: 'nav.aiSecurity',
      layoutVariant: 'app',
      path: '/ai/security'
    }),
    element: (
      <PermissionRoute permissionCode="ai:security:view">
        {lazyPage(<AiSecurityPage />)}
      </PermissionRoute>
    )
  },
  {
    path: 'ai/settings',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.aiSettings',
      cachePolicy: 'tab-alive',
      iconKey: 'settings',
      labelKey: 'nav.aiSettings',
      layoutVariant: 'app',
      path: '/ai/settings'
    }),
    element: (
      <PermissionRoute permissionCode="ai:settings:view">
        {lazyPage(<AiSettingsPage />)}
      </PermissionRoute>
    )
  },
  { path: 'ai/chat', element: <Navigate replace to="/ai/workbench" /> },
  { path: 'ai/conversations', element: <Navigate replace to="/ai/workbench?drawer=conversations" /> },
  { path: 'ai/prompt-templates', element: <Navigate replace to="/ai/capability?tab=prompts" /> },
  {
    path: 'ai/model-configs',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.aiModelConfigs',
      cachePolicy: 'tab-alive',
      iconKey: 'settings',
      labelKey: 'nav.aiModelConfigs',
      layoutVariant: 'app',
      path: '/ai/model-configs'
    }),
    element: (
      <PermissionRoute permissionCode="ai:model:view">
        {lazyPage(<AiCapabilityCenterPage allowedTabs={['models']} defaultTab="models" />)}
      </PermissionRoute>
    )
  },
  {
    path: 'ai/providers',
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.aiProviders',
      cachePolicy: 'tab-alive',
      iconKey: 'module',
      labelKey: 'nav.aiProviders',
      layoutVariant: 'app',
      path: '/ai/providers'
    }),
    element: (
      <PermissionRoute permissionCode="ai:provider:view">
        {lazyPage(<AiCapabilityCenterPage allowedTabs={['providers']} defaultTab="providers" />)}
      </PermissionRoute>
    )
  },
  { path: 'ai/agents', element: <Navigate replace to="/ai/capability?tab=agents" /> },
  { path: 'ai/usage', element: <Navigate replace to="/ai/observability?tab=overview" /> },
  { path: 'ai/logs', element: <Navigate replace to="/ai/observability?tab=runs" /> },
  { path: 'ai/knowledge', element: <Navigate replace to="/ai/capability?tab=knowledge" /> },
  { path: 'ai/sk-capabilities', element: <Navigate replace to="/ai/capability?tab=tools" /> }
];
