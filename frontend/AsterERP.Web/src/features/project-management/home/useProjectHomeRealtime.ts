import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';

import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { acquireProjectManagementHubConnection } from '../hooks/projectManagementHubConnection';
import type { ProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

interface HomeInvalidation {
  aggregateType?: string;
  aggregateId?: string;
  eventType?: string;
  projectId?: string;
  version?: number;
  sequence?: number;
}

export function useProjectHomeRealtime(signalRUrl: string, scope: ProjectManagementWorkspaceScope, enabled: boolean) {
  const queryClient = useQueryClient();
  const [connectionState, setConnectionState] = useState<'connected' | 'connecting' | 'reconnecting' | 'disconnected'>('disconnected');

  useEffect(() => {
    if (!enabled || !scope.isAvailable) return undefined;
    const connection = acquireProjectManagementHubConnection(signalRUrl, scope);
    if (!connection) return undefined;
    let timer: ReturnType<typeof setTimeout> | undefined;
    let disposed = false;
    let newestSequence = 0;
    const flush = () => {
      timer = undefined;
      if (disposed) return;
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.homeProjectsRoot(scope) });
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.homeSummaryRoot(scope) });
    };
    const onInvalidated = (event: HomeInvalidation) => {
      if (!event || (event.version ?? 0) <= 0) return;
      if ((event.sequence ?? event.version ?? 0) < newestSequence) return;
      newestSequence = Math.max(newestSequence, event.sequence ?? event.version ?? 0);
      if (!timer) timer = setTimeout(flush, 120);
    };
    const reconcile = () => {
      newestSequence = 0;
      if (!timer) timer = setTimeout(flush, 0);
    };
    connection.subscribe('ProjectManagementHomeInvalidated', onInvalidated);
    connection.subscribeLifecycle({
      reconnected: reconcile,
      stateChanged: setConnectionState,
    });
    return () => {
      disposed = true;
      if (timer) clearTimeout(timer);
      connection.dispose();
    };
  }, [enabled, queryClient, scope, signalRUrl]);

  return { connectionState };
}
