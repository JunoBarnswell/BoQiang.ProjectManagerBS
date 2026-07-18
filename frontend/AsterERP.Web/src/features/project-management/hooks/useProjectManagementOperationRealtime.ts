import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';

import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { getAccessToken } from '../../../core/http/tokenStorage';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import type { ProjectManagementOperation } from '../../../api/project-management/projectManagement.types';
import type { ProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

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
    if (!scope.isAvailable || !operationId) return undefined;
    setConnectionState('connecting');
    const connection = new HubConnectionBuilder()
      .withUrl(signalRUrl, { accessTokenFactory: () => getAccessToken() })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    const onProgress = (event: ProjectManagementOperationProgressEvent) => {
      if (event.id !== operationId) return;
      queryClient.setQueryData<ApiEnvelope<ProjectManagementOperation>>(
        projectManagementQueryKeys.operation(scope, operationId),
        (current) => current ? { ...current, data: { ...current.data, ...event } } : current,
      );
    };
    connection.on('ProjectManagementOperationProgressUpdated', onProgress);
    connection.onreconnecting(() => setConnectionState('reconnecting'));
    connection.onreconnected(() => {
      setConnectionState('connected');
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.operation(scope, operationId) });
    });
    connection.onclose(() => setConnectionState('disconnected'));
    void connection.start().then(() => setConnectionState('connected')).catch(() => setConnectionState('disconnected'));
    return () => {
      connection.off('ProjectManagementOperationProgressUpdated', onProgress);
      void connection.stop();
    };
  }, [operationId, queryClient, scope, signalRUrl]);
  return { connectionState };
}
