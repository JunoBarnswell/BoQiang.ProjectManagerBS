import { describe, expect, it } from 'vitest';

import {
  normalizeProjectManagementTargetRoute,
  projectManagementPlatformRoutePrefix,
  toProjectManagementPlatformRoute,
} from './projectManagementPlatformRoutes';

describe('projectManagementPlatformRoutes', () => {
  it('builds platform-owned PM routes without application workspace parameters', () => {
    expect(toProjectManagementPlatformRoute()).toBe(projectManagementPlatformRoutePrefix);
    expect(toProjectManagementPlatformRoute('/projects/project-a/overview')).toBe('/platform/project-management/projects/project-a/overview');
  });

  it('moves internal task search results into the canonical requirement editor route', () => {
    expect(normalizeProjectManagementTargetRoute('/projects/project-a/tasks?taskId=task-a'))
      .toBe('/platform/project-management/projects/project-a/requirements?view=tree&taskId=task-a');
    expect(normalizeProjectManagementTargetRoute('/project-search')).toBe('/project-search');
    expect(normalizeProjectManagementTargetRoute('/im/messages')).toBe('/im/messages');
  });
});
