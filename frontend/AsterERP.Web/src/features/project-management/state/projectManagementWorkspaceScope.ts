import { useMemo } from 'react';

import type { CurrentWorkspaceDto } from '../../../api/platform/auth.types';
import { useAuthStore, useWorkspaceStore } from '../../../core/state';

export interface ProjectManagementWorkspaceScope {
  appCode: string;
  isAvailable: boolean;
  tenantId: string;
  userId?: string;
}

type ProjectManagementWorkspace = Pick<CurrentWorkspaceDto, 'appCode' | 'tenantId' | 'workspaceLevel'>;

export function resolveProjectManagementWorkspaceScope(
  workspace: ProjectManagementWorkspace | null | undefined,
): ProjectManagementWorkspaceScope {
  const tenantId = workspace?.tenantId?.trim() ?? '';
  const appCode = workspace?.appCode?.trim().toUpperCase() ?? '';

  return {
    appCode,
    isAvailable: workspace?.workspaceLevel === 'platform' && tenantId.length > 0 && appCode === 'SYSTEM',
    tenantId,
  };
}

export function useProjectManagementWorkspaceScope(): ProjectManagementWorkspaceScope {
  const workspace = useWorkspaceStore((state) => state.currentWorkspace);
  const userId = useAuthStore((state) => state.user?.userId?.trim() ?? '');

  return useMemo(
    () => {
      const scope = resolveProjectManagementWorkspaceScope(workspace);
      return userId ? { ...scope, userId } : scope;
    },
    [userId, workspace],
  );
}
