import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';

import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { acquireProjectManagementHubConnection, type ProjectManagementHubConnectionState } from '../hooks/projectManagementHubConnection';
import type { ProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

interface ProjectInvalidationEvent {
  eventId?: string;
  eventType?: string;
  tenantId?: string;
  appCode?: string;
  projectId?: string;
  aggregateType?: string;
  aggregateId?: string;
  aggregateVersion?: number;
  workspaceSequence?: number;
  projectSequence?: number;
  changedFields?: string[];
  traceId?: string;
}

export function useProjectManagementProjectRealtime({ signalRUrl, scope, projectId, enabled, onAccessRevoked }: { signalRUrl: string; scope: ProjectManagementWorkspaceScope; projectId: string; enabled: boolean; onAccessRevoked?: () => void }) {
  const queryClient = useQueryClient();
  const [connectionState, setConnectionState] = useState<ProjectManagementHubConnectionState>('disconnected');

  useEffect(() => {
    if (!enabled || !scope.isAvailable || !projectId) return undefined;
    const connection = acquireProjectManagementHubConnection(signalRUrl, scope);
    if (!connection) return undefined;
    let timer: ReturnType<typeof setTimeout> | undefined;
    let disposed = false;
    let latestProjectSequence = 0;
    const aggregateVersions = new Map<string, number>();
    const reconcile = () => {
      latestProjectSequence = 0;
      aggregateVersions.clear();
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.overview(scope, { projectId, pageIndex: 1, pageSize: 1 }) });
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.milestones(scope, projectId) });
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.activities(scope, projectId, { pageIndex: 1, pageSize: 20 }) });
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.resources(scope, projectId) });
    };
    const flush = () => {
      timer = undefined;
      if (disposed) return;
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.overview(scope, { projectId, pageIndex: 1, pageSize: 1 }) });
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.milestones(scope, projectId) });
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.activities(scope, projectId, { pageIndex: 1, pageSize: 20 }) });
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.resources(scope, projectId) });
    };
    const queueFlush = () => { if (!timer) timer = setTimeout(flush, 120); };
    const unsubscribeInvalidation = connection.subscribe('ProjectManagementInvalidated', (payload: unknown) => {
      const event = readEvent(payload);
      if (!event || event.projectId !== projectId || (event.tenantId && event.tenantId !== scope.tenantId) || (event.appCode && event.appCode.toUpperCase() !== scope.appCode.toUpperCase())) return;
      const sequence = event.projectSequence ?? event.aggregateVersion ?? 0;
      const key = `${event.aggregateType ?? ''}:${event.aggregateId ?? ''}`;
      if (sequence > 0 && sequence < latestProjectSequence) return;
      if (sequence > 0) latestProjectSequence = Math.max(latestProjectSequence, sequence);
      const aggregateVersion = event.aggregateVersion ?? 0;
      if (aggregateVersion > 0 && (aggregateVersions.get(key) ?? 0) >= aggregateVersion) return;
      if (aggregateVersion > 0) aggregateVersions.set(key, aggregateVersion);
      queueFlush();
    });
    const unsubscribeRevocation = connection.subscribe('ProjectManagementAccessRevoked', (payload: unknown) => {
      const event = readEvent(payload);
      if (event?.projectId === projectId) onAccessRevoked?.();
    });
    const unsubscribeLifecycle = connection.subscribeLifecycle({ connected: reconcile, reconnected: reconcile, stateChanged: setConnectionState });
    const leaveProject = connection.subscribeProject(projectId, reconcile);
    return () => {
      disposed = true;
      unsubscribeInvalidation();
      unsubscribeRevocation();
      unsubscribeLifecycle();
      leaveProject();
      if (timer) clearTimeout(timer);
      connection.dispose();
    };
  }, [enabled, onAccessRevoked, projectId, queryClient, scope, signalRUrl]);

  return { connectionState };
}

function readEvent(payload: unknown): ProjectInvalidationEvent | undefined {
  if (!payload || typeof payload !== 'object') return undefined;
  const event = payload as ProjectInvalidationEvent;
  if (typeof event.projectId !== 'string' || !event.projectId) return undefined;
  return event;
}
