// @vitest-environment jsdom

import { matchRoutes, type RouteObject } from 'react-router-dom';
import { describe, expect, it } from 'vitest';

import { platformRoutes } from './routes/platformRoutes';
import { buildRuntimePageViewPermission } from './RuntimePagePermissionRoute';
import { applicationWorkspaceRoutes, workspaceRoutes } from './workspaceRoutes';
import { workspaceRoutes as targetApplicationWorkspaceRoutes } from './workspaceRoutes.target';

describe('applicationWorkspaceRoutes', () => {
  it('keeps low-code runtime pages available through the application admin dynamic route', () => {
    const runtimeRoutes = flattenRoutes(applicationWorkspaceRoutes).filter((item) => item.path === 'pages/:pageCode');
    const route = runtimeRoutes[0];
    const routeMeta = (route?.handle as { routeMeta?: { cachePolicy?: string; path?: string } } | undefined)?.routeMeta;

    expect(route).toBeDefined();
    expect(runtimeRoutes).toHaveLength(1);
    expect(route?.element).toBeDefined();
    expect(routeMeta?.cachePolicy).toBe('tab-alive');
    expect(routeMeta?.path).toBe('/tenants/:tenantId/apps/:appCode/admin/pages/:pageCode');
  });

  it.each([
    ['workflows/drafts', '/tenants/:tenantId/apps/:appCode/admin/workflows/drafts'],
    ['workflows/monitoring', '/tenants/:tenantId/apps/:appCode/admin/workflows/monitoring'],
    ['workflows/models', '/tenants/:tenantId/apps/:appCode/admin/workflows/models'],
    ['system/users', '/tenants/:tenantId/apps/:appCode/admin/system/users'],
    ['system/scheduled-jobs', '/tenants/:tenantId/apps/:appCode/admin/system/scheduled-jobs']
  ])('keeps fixed application shell menu route %s available under the application admin prefix', (routePath, expectedMetaPath) => {
    const route = flattenRoutes(applicationWorkspaceRoutes).find((item) => item.path === routePath);
    const routeMeta = (route?.handle as { routeMeta?: { path?: string } } | undefined)?.routeMeta;

    expect(route).toBeDefined();
    expect(route?.element).toBeDefined();
    expect(routeMeta?.path).toBe(expectedMetaPath);
  });

  it('uses the same normalized page permission segment as the backend runtime permission contract', () => {
    expect(buildRuntimePageViewPermission('Inventory_Page')).toBe('app:runtime-page:inventory-page:view');
    expect(buildRuntimePageViewPermission('  inventory-page  ')).toBe('app:runtime-page:inventory-page:view');
    expect(buildRuntimePageViewPermission(undefined)).toBe('app:runtime-page:unknown:view');
  });

  it('moves project overview to the platform workspace and safely redirects legacy application paths', () => {
    const platformOverviewRoute = flattenRoutes(platformRoutes).find((item) => item.path === 'platform/project-management/projects/:projectId/overview');
    const legacyOverviewRoute = flattenRoutes(workspaceRoutes).find((item) => item.path === 'projects/:projectId/overview');
    const applicationOverviewRoute = flattenRoutes(applicationWorkspaceRoutes).find((item) => item.path === 'projects/:projectId/overview');
    const permissionCode = (platformOverviewRoute?.element as { props?: { permissionCode?: string } } | undefined)?.props?.permissionCode;
    const legacyTarget = (legacyOverviewRoute?.element as { props?: { to?: string } } | undefined)?.props?.to;
    const applicationTarget = (applicationOverviewRoute?.element as { props?: { to?: string } } | undefined)?.props?.to;

    expect(platformOverviewRoute).toBeDefined();
    expect(permissionCode).toBe('project-management:project:view');
    expect(legacyTarget).toBe('/platform/project-management');
    expect(applicationTarget).toBe('/platform/project-management');
  });

  it('keeps the platform PM route registered in an application-target frontend build', () => {
    const matches = matchRoutes([{ children: targetApplicationWorkspaceRoutes, path: '/' }], '/platform/project-management');
    const platformRoute = flattenRoutes(targetApplicationWorkspaceRoutes).find((item) => item.path === 'platform/project-management');

    expect(matches?.at(-1)?.route).toBe(platformRoute);
    expect((platformRoute?.element as { props?: { permissionCode?: string } } | undefined)?.props?.permissionCode)
      .toBe('project-management:project:view');
  });

  it('keeps the latest Page Studio entry actions behind designer permissions', () => {
    const source = Object.values(import.meta.glob('../../pages/application-console/development-center/ApplicationDevelopmentPagesPage.tsx', { eager: true, import: 'default', query: '?raw' }))[0] as string;
    const pageBoardSource = Object.values(import.meta.glob('../../pages/application-console/development-center/PageBoard.tsx', { eager: true, import: 'default', query: '?raw' }))[0] as string;

    expect(source).toContain("edit: 'app:development-center:designer:edit'");
    expect(source).toContain("preview: 'app:development-center:designer:preview'");
    expect(source).toContain("publish: 'app:development-center:designer:publish'");
    expect(pageBoardSource).toContain("view: 'app:development-center:designer:view'");
    expect(pageBoardSource).toContain('<PermissionButton code={designerPermissions.edit}');
    expect(pageBoardSource).toContain("delete: 'app:development-center:designer:delete'");
    expect(pageBoardSource).toContain('code={designerPermissions.delete}');
    expect(pageBoardSource).toContain('onDeletePage');
    expect(pageBoardSource).toContain('<PermissionButton code={designerPermissions.preview}');
    expect(pageBoardSource).toContain('code={designerPermissions.publish}');
    expect(pageBoardSource).not.toContain('<PermissionButton code={designerPermissions.view}');
    expect(pageBoardSource).not.toContain('<PermissionButton code={designerPermissions.edit} className="flex');
    expect(source).not.toMatch(/<button[^>]*onClick=\{onEdit\}/);
    expect(source).not.toMatch(/<button[^>]*onClick=\{onDesign\}/);
  });
});

function flattenRoutes(routes: RouteObject[]): RouteObject[] {
  return routes.flatMap((route) => [route, ...flattenRoutes(route.children ?? [])]);
}
