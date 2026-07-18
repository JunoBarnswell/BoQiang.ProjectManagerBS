import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';

import type { ProjectManagementOperation } from '../../../api/project-management/projectManagement.types';
import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import type { ProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

import { acquireProjectManagementHubConnection } from './projectManagementHubConnection';

interface ProjectManagementOperationProgressEvent {
  id: string;
  operationType: string;
  status: string;
  phase: string;
  progressPercent: number;
  isCancellationRequested: boolean;
  completedTime?: string;
}

export function useProjectManagementOperationRealtime(
  signalRUrl: string,
  scope: ProjectManagementWorkspaceScope,
  operationId: string | undefined,
) {
  const queryClient = useQueryClient();
  const [connectionState, setConnectionState] = useState<'connected' | 'connecting' | 'reconnecting' | 'disconnected'>('disconnected');

  useEffect(() => {
    if (!scope.isAvailable || !operationId) {
      setConnectionState('disconnected');
      return undefined;
    }
    const connection = acquireProjectManagementHubConnection(signalRUrl, scope);
    if (!connection) return undefined;
    const onProgress = (event: ProjectManagementOperationProgressEvent) => {
      if (event.id !== operationId) return;
      queryClient.setQueryData<ApiEnvelope<ProjectManagementOperation>>(
        projectManagementQueryKeys.operation(scope, operationId),
        (current) => current ? { ...current, data: { ...current.data, ...event } } : current,
      );
    };
    connection.subscribe('ProjectManagementOperationProgressUpdated', onProgress);
    connection.subscribeLifecycle({
      reconnected: () => {
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.operation(scope, operationId) });
      },
      stateChanged: setConnectionState,
    });
    return () => {
      connection.dispose();
    };
  }, [operationId, queryClient, scope, signalRUrl]);
  return { connectionState };
}
