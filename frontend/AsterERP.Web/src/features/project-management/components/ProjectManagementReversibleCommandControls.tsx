import { useQuery, useQueryClient } from '@tanstack/react-query';

import {
  getProjectManagementReversibleCommandStack,
  redoProjectManagementReversibleCommand,
  undoProjectManagementReversibleCommand,
} from '../../../api/project-management/projectManagement.api';
import { usePermission } from '../../../core/auth/usePermission';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { useMessage } from '../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

function createRequestId(): string {
  return globalThis.crypto?.randomUUID?.() ?? `reversible-command-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

export function ProjectManagementReversibleCommandControls() {
  const scope = useProjectManagementWorkspaceScope();
  const { hasPermission: canView } = usePermission('project-management:reversible-command:view');
  const queryClient = useQueryClient();
  const message = useMessage();
  const stackQuery = useQuery({
    enabled: scope.isAvailable && canView,
    queryFn: ({ signal }) => getProjectManagementReversibleCommandStack(signal),
    queryKey: projectManagementQueryKeys.reversibleCommands(scope),
  });
  const refreshWorkspace = async () => {
    await queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.all(scope) });
  };
  const undoMutation = useApiMutation({
    mutationFn: () => undoProjectManagementReversibleCommand({ requestId: createRequestId() }),
    onError: (error) => message.error(getErrorMessage(error, '撤销失败，请刷新后重试')),
    onSuccess: async (result) => {
      message.success(result.data?.summary ? `已撤销：${result.data.summary}` : '已撤销最近一次业务操作');
      await refreshWorkspace();
    },
  });
  const redoMutation = useApiMutation({
    mutationFn: () => redoProjectManagementReversibleCommand({ requestId: createRequestId() }),
    onError: (error) => message.error(getErrorMessage(error, '重做失败，请刷新后重试')),
    onSuccess: async (result) => {
      message.success(result.data?.summary ? `已重做：${result.data.summary}` : '已重做最近一次业务操作');
      await refreshWorkspace();
    },
  });

  if (!canView) return null;

  const stack = stackQuery.data?.data;
  const pending = undoMutation.isPending || redoMutation.isPending;
  const latest = stack?.commands[0];
  return (
    <div aria-label="业务撤销重做" className="flex flex-wrap items-center gap-2 text-sm">
      <PermissionButton
        code="project-management:reversible-command:manage"
        disabled={pending || stackQuery.isLoading || !stack?.canUndo}
        onClick={() => undoMutation.mutate()}
      >
        {undoMutation.isPending ? '撤销中…' : '撤销'}
      </PermissionButton>
      <PermissionButton
        code="project-management:reversible-command:manage"
        disabled={pending || stackQuery.isLoading || !stack?.canRedo}
        onClick={() => redoMutation.mutate()}
      >
        {redoMutation.isPending ? '重做中…' : '重做'}
      </PermissionButton>
      {stackQuery.isError ? <button className="text-xs text-amber-700 underline" onClick={() => void stackQuery.refetch()} type="button">撤销栈加载失败，重试</button> : null}
      {!stackQuery.isError && stack ? <span className="text-xs text-gray-500" title={latest?.summary ?? undefined}>最近 {stack.commands.length} 步{latest?.summary ? ` · ${latest.summary}` : ''}</span> : null}
    </div>
  );
}
