import { useMemo } from 'react';

import { useWorkspaceStore } from '../../../core/state/workspaceStore';

export interface ProjectManagementWorkspaceScope {
  appCode: string;
  isAvailable: boolean;
  tenantId: string;
}

export function useProjectManagementWorkspaceScope(): ProjectManagementWorkspaceScope {
  const workspace = useWorkspaceStore((state) => state.currentWorkspace);

  return useMemo(() => {
    const tenantId = workspace?.tenantId?.trim() ?? '';
    const appCode = workspace?.appCode?.trim().toUpperCase() ?? '';
    return {
      appCode,
      isAvailable: tenantId.length > 0 && appCode.length > 0,
      tenantId,
    };
  }, [workspace?.appCode, workspace?.tenantId]);
}
