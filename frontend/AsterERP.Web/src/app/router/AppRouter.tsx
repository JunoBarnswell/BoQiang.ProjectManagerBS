import { lazy, Suspense, type ReactNode } from 'react';
import { createBrowserRouter, Navigate, type RouteObject } from 'react-router-dom';

import { applicationWorkspaceRoutes, workspaceRoutes } from '@/app/router/workspaceRoutes';

import { Page404 } from '../../shared/status/Page404';
import { PageLoading } from '../../shared/status/PageLoading';
import { AppLayout } from '../layout/AppLayout';

import { ApplicationWorkspaceRoute } from './ApplicationWorkspaceRoute';
import { AuthRoute } from './AuthRoute';
import { PermissionRoute } from './PermissionRoute';

const LoginPage = lazy(() => import('../../pages/auth/LoginPage').then((module) => ({ default: module.LoginPage })));
const ApplicationLoginPage = lazy(() => import('../../pages/auth/ApplicationLoginPage').then((module) => ({ default: module.ApplicationLoginPage })));
const WorkspaceSelectPage = lazy(() => import('../../pages/workspace/WorkspaceSelectPage').then((module) => ({ default: module.WorkspaceSelectPage })));
const AsterSceneExplorePage = lazy(() =>
  import('../../features/aster-scene/pages/AsterSceneExplorePage').then((module) => ({ default: module.AsterSceneExplorePage }))
);
const AsterSceneTemplatesPage = lazy(() =>
  import('../../features/aster-scene/pages/AsterSceneTemplatesPage').then((module) => ({ default: module.AsterSceneTemplatesPage }))
);
const AsterSceneWorkPage = lazy(() =>
  import('../../features/aster-scene/pages/AsterSceneWorkPage').then((module) => ({ default: module.AsterSceneWorkPage }))
);
const AsterSceneCreatorPage = lazy(() =>
  import('../../features/aster-scene/pages/AsterSceneCreatorPage').then((module) => ({ default: module.AsterSceneCreatorPage }))
);
const AsterScenePlayerPage = lazy(() =>
  import('../../features/aster-scene/pages/AsterScenePlayerPage').then((module) => ({ default: module.AsterScenePlayerPage }))
);
const AsterScenePricingPage = lazy(() =>
  import('../../features/aster-scene/pages/AsterScenePricingPage').then((module) => ({ default: module.AsterScenePricingPage }))
);
const AsterSceneStudioPage = lazy(() =>
  import('../../features/aster-scene/pages/AsterSceneStudioPage').then((module) => ({ default: module.AsterSceneStudioPage }))
);
const retiredSceneRoute = '/virtual' + '-exhibition/*';
const workspaceAwareBasePath = resolveWorkspaceAwareBasePath();

function lazyPage(children: ReactNode) {
  return <Suspense fallback={<PageLoading />}>{children}</Suspense>;
}

function ApplicationShortRouteRedirect() {
  return <Navigate replace to="/platform/applications" />;
}

function buildRoutes(): RouteObject[] {
  return [
    {
      path: '/login',
      element: lazyPage(<LoginPage />)
    },
    {
      path: '/tenants/:tenantId/apps/:appCode/login',
      element: lazyPage(<ApplicationLoginPage />)
    },
    {
      path: '/explore',
      element: lazyPage(<AsterSceneExplorePage />)
    },
    {
      path: '/templates',
      element: lazyPage(<AsterSceneTemplatesPage />)
    },
    {
      path: '/works/:slug',
      element: lazyPage(<AsterSceneWorkPage />)
    },
    {
      path: '/creator/:handle',
      element: lazyPage(<AsterSceneCreatorPage />)
    },
    {
      path: '/player/:publishCode',
      element: lazyPage(<AsterScenePlayerPage />)
    },
    {
      path: '/pricing',
      element: lazyPage(<AsterScenePricingPage />)
    },
    {
      path: retiredSceneRoute,
      element: <Page404 />
    },
    {
      children: [
        {
          element: <AuthRoute />,
          children: [
            {
              path: 'workspace',
              element: lazyPage(<WorkspaceSelectPage />)
            },
            {
              path: 'studio/:projectId',
              element: lazyPage(
                <PermissionRoute permissionCode="asterscene:studio:open">
                  <AsterSceneStudioPage />
                </PermissionRoute>
              )
            },
            {
              children: [
                {
                  element: <ApplicationShortRouteRedirect />,
                  path: 'apps/:appCode'
                },
                {
                  element: <ApplicationShortRouteRedirect />,
                  path: 'apps/:appCode/:consoleSlug'
                },
                {
                  children: applicationWorkspaceRoutes,
                  element: <ApplicationWorkspaceRoute />,
                  path: 'tenants/:tenantId/apps/:appCode/admin'
                },
                ...workspaceRoutes
              ],
              element: <AppLayout />,
              path: '/'
            }
          ]
        }
      ],
      path: '/'
    }
  ];
}

export const appRouter = createBrowserRouter(buildRoutes(), {
  basename: workspaceAwareBasePath === '/' ? undefined : workspaceAwareBasePath
});

function resolveWorkspaceAwareBasePath(): string {
  const path = window.location.pathname;
  const firstSegment = path.split('/').filter(Boolean)[0];
  if (!firstSegment) {
    return '/';
  }

  if (firstSegment === 'tenants' || firstSegment === 'workspace' || firstSegment === 'login' || firstSegment === 'explore' || firstSegment.startsWith('assets')) {
    return '/';
  }

  if (firstSegment === firstSegment.toUpperCase() && /^[A-Z0-9][A-Z0-9_-]*$/.test(firstSegment)) {
    return `/${firstSegment}`;
  }

  return '/';
}
