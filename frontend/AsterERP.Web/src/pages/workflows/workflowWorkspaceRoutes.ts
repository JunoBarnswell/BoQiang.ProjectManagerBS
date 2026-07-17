export interface WorkflowWorkspaceRouteContext {
  appCode?: string | null;
  tenantId?: string | null;
}

export function buildWorkspaceRoute(
  routePath: string | null | undefined,
  workspace: WorkflowWorkspaceRouteContext | null | undefined
): string {
  const normalized = routePath?.trim()
    ? routePath.trim().startsWith('/') ? routePath.trim() : `/${routePath.trim()}`
    : '/home';
  if (normalized.startsWith('/tenants/')) {
    return normalized;
  }

  const tenantId = workspace?.tenantId?.trim();
  const appCode = workspace?.appCode?.trim();
  if (!tenantId || !appCode) {
    return normalized;
  }

  const workspacePath = normalized.startsWith('/admin/')
    ? normalized.slice('/admin'.length)
    : normalized;
  return `/tenants/${encodeURIComponent(tenantId)}/apps/${encodeURIComponent(appCode)}/admin${workspacePath}`;
}
