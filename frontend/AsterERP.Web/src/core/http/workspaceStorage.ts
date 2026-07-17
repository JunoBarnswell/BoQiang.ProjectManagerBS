import type { CurrentWorkspaceDto } from '../../api/platform/auth.types';

const workspaceStorageKey = 'astererp.currentWorkspace';

export function getStoredWorkspace(): CurrentWorkspaceDto | null {
  const rawValue = localStorage.getItem(workspaceStorageKey);
  if (!rawValue) {
    return null;
  }

  try {
    const parsed = JSON.parse(rawValue) as Partial<CurrentWorkspaceDto>;
    if (!parsed || typeof parsed.tenantId !== 'string' || typeof parsed.appCode !== 'string') {
      return null;
    }

    const workspaceId = parsed.workspaceId || parsed.systemId || `${parsed.tenantId}:${parsed.appCode}`;
    return {
      appCode: parsed.appCode,
      appName: parsed.appName ?? parsed.systemName ?? parsed.appCode,
      defaultRoutePath: parsed.defaultRoutePath ?? null,
      systemCode: parsed.systemCode ?? parsed.appCode,
      systemId: parsed.systemId ?? workspaceId,
      systemName: parsed.systemName ?? parsed.appName ?? parsed.appCode,
      tenantId: parsed.tenantId,
      tenantName: parsed.tenantName ?? parsed.tenantId,
      workspaceId,
      workspaceLevel: parsed.workspaceLevel === 'application' ? 'application' : 'platform'
    };
  } catch {
    return null;
  }
}

export function setStoredWorkspace(workspace: CurrentWorkspaceDto | null): void {
  if (!workspace) {
    localStorage.removeItem(workspaceStorageKey);
    return;
  }

  localStorage.setItem(workspaceStorageKey, JSON.stringify(workspace));
}
