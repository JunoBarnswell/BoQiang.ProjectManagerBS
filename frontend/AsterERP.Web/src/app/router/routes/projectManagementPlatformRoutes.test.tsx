// @vitest-environment jsdom

import type { RouteObject } from 'react-router-dom';
import { describe, expect, it } from 'vitest';

import { projectManagementPlatformRoutes } from './projectManagementPlatformRoutes';

describe('projectManagementPlatformRoutes', () => {
  it('allows either sync export or import permission to enter the sync page', () => {
    const syncRoute = projectManagementPlatformRoutes.find((route) => route.path === 'platform/project-management/project-sync');

    expect(permissionCode(syncRoute)).toEqual([
      'project-management:sync:export',
      'project-management:sync:import',
    ]);
  });

  it('keeps non-sync platform PM route permissions unchanged', () => {
    const auditRoute = projectManagementPlatformRoutes.find((route) => route.path === 'platform/project-management/project-audit-center');
    const overviewRoute = projectManagementPlatformRoutes.find((route) => route.path === 'platform/project-management/projects/:projectId/overview');

    expect(permissionCode(auditRoute)).toBe('project-management:audit:view');
    expect(permissionCode(overviewRoute)).toBe('project-management:project:view');
  });
});

function permissionCode(route: RouteObject | undefined): string | string[] | undefined {
  return (route?.element as { props?: { permissionCode?: string | string[] } } | undefined)?.props?.permissionCode;
}
