import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';

import { cancelProjectManagementOperation, getProjectManagementOperation } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementOperation } from '../../../api/project-management/projectManagement.types';
import { isHttpError } from '../../../core/http/httpError';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { useMessage } from '../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { useProjectManagementOperationRealtime } from '../hooks/useProjectManagementOperationRealtime';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

export function ProjectManagementOperationProgress({ operationId, clearOnTerminal = true, onChanged, onTerminal, onTrackingEnded }: { operationId: string; clearOnTerminal?: boolean; onChanged?: () => void; onTerminal?: (operation: ProjectManagementOperation) => void; onTrackingEnded?: () => void }) {
  const scope = useProjectManagementWorkspaceScope();
  const queryClient = useQueryClient();
  const message = useMessage();
  const queryKey = projectManagementQueryKeys.operation(scope, operationId);
  const operationQuery = useQuery({
    enabled: scope.isAvailable && Boolean(operationId),
    queryKey,
    queryFn: ({ signal }) => getProjectManagementOperation(operationId, signal),
    refetchInterval: (query) => ['Pending', 'Running'].includes(query.state.data?.data.status ?? '') ? 5_000 : false,
  });
  const { connectionState } = useProjectManagementOperationRealtime('/hubs/system-notification', scope, operationId);
  const cancelMutation = useApiMutation({
    mutationFn: () => cancelProjectManagementOperation(operationId),
    onError: (error) => message.error(getErrorMessage(error, '取消长任务失败')),
    onSuccess: (result) => {
      queryClient.setQueryData(queryKey, result);
      onChanged?.();
    }
  });
  const operation = operationQuery.data?.data;
  useEffect(() => {
    if (operation && !['Pending', 'Running'].includes(operation.status)) {
      onTerminal?.(operation);
      if (clearOnTerminal) onTrackingEnded?.();
      return;
    }
    if (operationQuery.isError && isHttpError(operationQuery.error) && operationQuery.error.status === 404) onTrackingEnded?.();
  }, [clearOnTerminal, onTerminal, onTrackingEnded, operation, operationQuery.error, operationQuery.isError]);
  if (operationQuery.isLoading) return <p role="status" className="text-sm text-gray-500">正在加载长任务状态…</p>;
  if (operationQuery.isError || !operation) {
    const description = isHttpError(operationQuery.error) && operationQuery.error.status === 403 ? '无权查看该长任务。'
      : isHttpError(operationQuery.error) && operationQuery.error.status === 404 ? '长任务不存在或已失效。'
        : '长任务状态暂时无法加载。';
    return <p role="alert" className="text-sm text-amber-700">{description} <button type="button" className="underline" onClick={() => void operationQuery.refetch()}>重试</button></p>;
  }
  const canCancel = ['Pending', 'Running'].includes(operation.status) && !operation.isCancellationRequested;
  return <section aria-live="polite" aria-busy={operationQuery.isFetching} className="rounded-lg border border-sky-200 bg-sky-50 p-3 text-sm">
    <div className="flex flex-wrap items-center justify-between gap-2"><strong>{operation.operationType}</strong><span>{operation.status}</span></div>
    <p className="mt-1 text-slate-600">{operation.phase} · {operation.progressPercent}%</p>
    <progress aria-label="长任务进度" className="mt-2 w-full" max={100} value={operation.progressPercent} />
    <p className="mt-1 text-xs text-slate-500">实时连接：{connectionState === 'connected' ? '已连接' : connectionState === 'reconnecting' ? '重连中，正在回补状态' : '轮询回补中'}</p>
    {operation.isCancellationRequested ? <p className="mt-2 text-amber-700">已请求取消，正在等待安全检查点结束。</p> : null}
    {operation.errorMessage ? <p className="mt-2 text-red-700">{operation.errorMessage}</p> : null}
    {canCancel ? <PermissionButton className="mt-2" code="project-management:operation:manage" disabled={cancelMutation.isPending} onClick={() => cancelMutation.mutate()}>取消任务</PermissionButton> : null}
  </section>;
}
