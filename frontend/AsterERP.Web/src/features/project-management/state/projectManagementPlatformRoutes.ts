export const projectManagementPlatformRoutePrefix = '/platform/project-management';

export const projectManagementRoutePaths = [
  'projects/:projectId/overview',
  'projects/:projectId/requirements',
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
  const taskMatch = /^projects\/([^/]+)\/tasks(?:\?taskId=([^&]+))?$/.exec(route);
  if (taskMatch) {
    const query = taskMatch[2] ? `?view=tree&taskId=${taskMatch[2]}` : '?view=tree';
    return `${projectManagementPlatformRoutePrefix}/projects/${taskMatch[1]}/requirements${query}`;
  }
  return route.startsWith('projects/')
    ? toProjectManagementPlatformRoute(route)
    : targetRoute;
}
