export function normalizeWorkflowMenuPath(routePath?: string | null): string {
  if (routePath === '/flowise/agentflows') {
    return '/flowise/workflows';
  }

  return routePath ?? '/home';
}

export function formatWorkflowMenuGroupLabel(fallback: string): string {
  return fallback;
}

export function formatWorkflowMenuTitle(_path: string, fallback: string): string {
  return fallback;
}

export function formatWorkflowMenuDesc(_path: string, fallback?: string | null): string {
  return fallback ?? '';
}
