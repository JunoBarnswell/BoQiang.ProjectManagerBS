export const projectManagementPlatformRoutePrefix = '/platform/project-management';

export const projectManagementRoutePaths = [
  'projects/:projectId/overview',
  'projects/:projectId/requirements',
  'projects/:projectId/members',
] as const;

export function isProjectManagementWorkbenchPath(pathname: string): boolean {
  return pathname === projectManagementPlatformRoutePrefix || pathname.startsWith(`${projectManagementPlatformRoutePrefix}/`);
}

export function toProjectManagementPlatformRoute(path = ''): string {
  const normalizedPath = path.trim().replace(/^\/+/, '');
  return normalizedPath ? `${projectManagementPlatformRoutePrefix}/${normalizedPath}` : projectManagementPlatformRoutePrefix;
}

export function normalizeProjectManagementTargetRoute(targetRoute: string): string {
  if (!targetRoute.startsWith('/')) return targetRoute;

  const route = targetRoute.slice(1);
  const taskMatch = /^projects\/([^/]+)\/tasks(?:\?(.+))?$/.exec(route);
  if (taskMatch) {
    const params = new URLSearchParams(taskMatch[2] ?? '');
    const taskId = params.get('taskId') ?? params.get('selectedTaskId');
    const focus = params.get('focus');
    const query = new URLSearchParams({ view: 'tree' });
    if (taskId) query.set('taskId', taskId);
    if (focus) query.set('focus', focus);
    return `${projectManagementPlatformRoutePrefix}/projects/${taskMatch[1]}/requirements?${query.toString()}`;
  }
  return route.startsWith('projects/')
    ? toProjectManagementPlatformRoute(route)
    : targetRoute;
}
