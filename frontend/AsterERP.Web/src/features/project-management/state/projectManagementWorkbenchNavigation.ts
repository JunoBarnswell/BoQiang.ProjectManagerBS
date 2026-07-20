import type { ProjectManagementTaskView } from '../../../api/project-management/projectManagement.types';

import { projectManagementPlatformRoutePrefix } from './projectManagementPlatformRoutes';

export type ProjectManagementWorkbenchArea = 'projects' | 'my-work' | 'search' | 'recycle-bin' | 'sync' | 'audit';
export type ProjectManagementProjectSection = 'overview' | 'tasks' | 'milestones' | 'members' | 'reports' | 'settings';

export interface ProjectManagementWorkbenchRoute {
  area: ProjectManagementWorkbenchArea;
  projectId?: string;
  projectSection?: ProjectManagementProjectSection;
  taskView?: ProjectManagementTaskView;
}

const taskViewBySegment: Record<string, ProjectManagementTaskView> = {
  board: 'board',
  calendar: 'calendar',
  card: 'card',
  gantt: 'gantt',
  list: 'list',
  tasks: 'tree',
};

const projectSections = new Set<ProjectManagementProjectSection>(['overview', 'tasks', 'milestones', 'members', 'reports', 'settings']);

export function isProjectManagementWorkbenchPath(pathname: string): boolean {
  return pathname === projectManagementPlatformRoutePrefix || pathname.startsWith(`${projectManagementPlatformRoutePrefix}/`);
}

export function parseProjectManagementWorkbenchRoute(pathname: string): ProjectManagementWorkbenchRoute {
  const suffix = pathname.slice(projectManagementPlatformRoutePrefix.length).replace(/^\/+/, '');
  const segments = suffix.split('/').filter(Boolean);

  if (segments[0] === 'projects' && segments[1]) {
    const projectId = decodeURIComponent(segments[1]);
    const segment = segments[2] ?? 'overview';
    const taskView = taskViewBySegment[segment];
    if (taskView) return { area: 'projects', projectId, projectSection: 'tasks', taskView };
    if (projectSections.has(segment as ProjectManagementProjectSection)) {
      return { area: 'projects', projectId, projectSection: segment as ProjectManagementProjectSection };
    }
    return { area: 'projects', projectId, projectSection: 'overview' };
  }

  if (segments[0] === 'my-work') return { area: 'my-work' };
  if (segments[0] === 'project-search') return { area: 'search' };
  if (segments[0] === 'project-recycle-bin') return { area: 'recycle-bin' };
  if (segments[0] === 'project-sync') return { area: 'sync' };
  if (segments[0] === 'project-audit-center') return { area: 'audit' };
  return { area: 'projects' };
}

export function projectManagementWorkbenchPath(route: ProjectManagementWorkbenchRoute): string {
  if (route.area === 'my-work') return `${projectManagementPlatformRoutePrefix}/my-work`;
  if (route.area === 'search') return `${projectManagementPlatformRoutePrefix}/project-search`;
  if (route.area === 'recycle-bin') return `${projectManagementPlatformRoutePrefix}/project-recycle-bin`;
  if (route.area === 'sync') return `${projectManagementPlatformRoutePrefix}/project-sync`;
  if (route.area === 'audit') return `${projectManagementPlatformRoutePrefix}/project-audit-center`;
  if (!route.projectId) return projectManagementPlatformRoutePrefix;

  const projectPrefix = `${projectManagementPlatformRoutePrefix}/projects/${encodeURIComponent(route.projectId)}`;
  if (route.projectSection === 'tasks') return `${projectPrefix}/tasks`;
  return `${projectPrefix}/${route.projectSection ?? 'overview'}`;
}

export function projectManagementTaskViewFromSearch(search: string, fallback: ProjectManagementTaskView): ProjectManagementTaskView {
  const view = new URLSearchParams(search).get('view');
  return view && Object.values(taskViewBySegment).includes(view as ProjectManagementTaskView)
    ? view as ProjectManagementTaskView
    : fallback;
}
