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

  it('normalizes legacy PM targets while retaining non-PM targets', () => {
    expect(normalizeProjectManagementTargetRoute('/projects/project-a/tasks?taskId=task-a'))
      .toBe('/platform/project-management/projects/project-a/tasks?taskId=task-a');
    expect(normalizeProjectManagementTargetRoute('/project-search')).toBe('/platform/project-management/project-search');
    expect(normalizeProjectManagementTargetRoute('/im/messages')).toBe('/im/messages');
  });
});
