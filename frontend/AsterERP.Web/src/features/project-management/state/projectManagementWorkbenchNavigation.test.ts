import { describe, expect, it } from 'vitest';

import { parseProjectManagementWorkbenchRoute, projectManagementTaskViewFromSearch, projectManagementWorkbenchPath } from './projectManagementWorkbenchNavigation';

describe('projectManagementWorkbenchNavigation', () => {
  it('keeps legacy task views in the project task section', () => {
    expect(parseProjectManagementWorkbenchRoute('/platform/project-management/projects/p-1/board')).toEqual({
      area: 'projects',
      projectId: 'p-1',
      projectSection: 'tasks',
      taskView: 'board',
    });
  });

  it('uses the canonical task path for inline view switching', () => {
    expect(projectManagementWorkbenchPath({ area: 'projects', projectId: 'p-1', projectSection: 'tasks' }))
      .toBe('/platform/project-management/projects/p-1/tasks');
    expect(projectManagementTaskViewFromSearch('?view=calendar', 'tree')).toBe('calendar');
  });

  it('uses the existing HOME route when no project is selected', () => {
    expect(projectManagementWorkbenchPath({ area: 'projects' })).toBe('/platform/project-management');
  });

  it('resolves the project settings section instead of the task fallback', () => {
    expect(parseProjectManagementWorkbenchRoute('/platform/project-management/projects/p-1/settings')).toEqual({
      area: 'projects',
      projectId: 'p-1',
      projectSection: 'settings',
    });
  });
});
