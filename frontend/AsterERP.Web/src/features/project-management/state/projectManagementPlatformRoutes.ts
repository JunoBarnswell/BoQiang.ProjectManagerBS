export const projectManagementPlatformRoutePrefix = '/platform/project-management';

export const projectManagementRoutePaths = [
  'projects',
  'my-work',
  'projects/:projectId/overview',
  'projects/:projectId/members',
  'projects/:projectId/tasks',
  'projects/:projectId/list',
  'projects/:projectId/card',
  'projects/:projectId/board',
  'projects/:projectId/gantt',
  'projects/:projectId/calendar',
  'projects/:projectId/milestones',
  'projects/:projectId/reports',
  'projects/:projectId/settings',
  'project-search',
  'project-sync',
  'project-recycle-bin',
  'project-audit-center',
] as const;

export function toProjectManagementPlatformRoute(path = ''): string {
  const normalizedPath = path.trim().replace(/^\/+/, '');
  return normalizedPath ? `${projectManagementPlatformRoutePrefix}/${normalizedPath}` : projectManagementPlatformRoutePrefix;
}

export function normalizeProjectManagementTargetRoute(targetRoute: string): string {
  if (!targetRoute.startsWith('/')) return targetRoute;

  const route = targetRoute.slice(1);
  return route === 'projects' ||
    route.startsWith('projects/') ||
    route === 'my-work' ||
    route === 'project-search' ||
    route === 'project-sync' ||
    route === 'project-recycle-bin' ||
    route === 'project-audit-center'
    ? toProjectManagementPlatformRoute(route)
    : targetRoute;
}
