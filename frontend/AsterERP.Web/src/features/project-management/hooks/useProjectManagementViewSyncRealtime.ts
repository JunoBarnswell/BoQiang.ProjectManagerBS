import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';

import type { ProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';
import {
  isProjectManagementViewSyncInvalidation,
  shouldClearProjectManagementViewSyncSelection,
  type ProjectManagementViewSyncInvalidation,
} from '../view-sync/projectManagementViewSyncModel';

import { acquireProjectManagementHubConnection } from './projectManagementHubConnection';
import { invalidateProjectManagementViewSyncCaches } from './useProjectManagementViewSync';

interface ProjectManagementViewSyncRealtimeOptions {
  enabled: boolean;
  onReconciled?: () => void;
  onSelectedTaskDeleted?: (taskId: string) => void;
  projectId: string;
  selectedTaskId?: string;
  signalRUrl: string;
  scope: ProjectManagementWorkspaceScope;
}

/**
 * Project-scoped SignalR bridge for all task views. It accepts only the current tenant/app hub lease,
 * filters the subscribed project again, and coalesces duplicate aggregate versions before cache refresh.
 */
export function useProjectManagementViewSyncRealtime({ enabled, onReconciled, onSelectedTaskDeleted, projectId, selectedTaskId, signalRUrl, scope }: ProjectManagementViewSyncRealtimeOptions): void {
  const queryClient = useQueryClient();
  const selectedTaskIdRef = useRef(selectedTaskId);
  selectedTaskIdRef.current = selectedTaskId;

  useEffect(() => {
    if (!enabled || !scope.isAvailable || !projectId) return undefined;
    const versions = new Map<string, number>();
    const connection = acquireProjectManagementHubConnection(signalRUrl, scope);
    if (!connection) return undefined;

    const unsubscribeInvalidation = connection.subscribe('ProjectManagementInvalidated', (payload: unknown) => {
      if (!isProjectManagementViewSyncInvalidation(payload) || payload.projectId !== projectId || payload.version <= 0) return;
      const versionKey = `${payload.aggregateType}:${payload.aggregateId}`;
      if ((versions.get(versionKey) ?? 0) >= payload.version) return;
      versions.set(versionKey, payload.version);
      if (shouldClearProjectManagementViewSyncSelection(payload, selectedTaskIdRef.current)) onSelectedTaskDeleted?.(payload.aggregateId);
      void invalidateProjectManagementViewSyncCaches(queryClient, { projectId, scope }, payload);
    });
    const unsubscribeRevocation = connection.subscribe('ProjectManagementAccessRevoked', (payload: unknown) => {
      const event = readProjectAccessRevocation(payload);
      if (!event || event.projectId !== projectId) return;
      versions.clear();
      if (selectedTaskIdRef.current) onSelectedTaskDeleted?.(selectedTaskIdRef.current);
      onReconciled?.();
      void invalidateProjectManagementViewSyncCaches(queryClient, { projectId, scope }, event);
    });
    const leaveProject = connection.subscribeProject(projectId, () => {
      versions.clear();
      onReconciled?.();
    });
    return () => {
      unsubscribeInvalidation();
      unsubscribeRevocation();
      leaveProject();
      connection.dispose();
    };
  }, [enabled, onReconciled, onSelectedTaskDeleted, projectId, queryClient, scope, signalRUrl]);
}

function readProjectAccessRevocation(payload: unknown): ProjectManagementViewSyncInvalidation | undefined {
  if (!payload || typeof payload !== 'object' || typeof (payload as { projectId?: unknown }).projectId !== 'string') return undefined;
  const projectId = (payload as { projectId: string }).projectId;
  return { aggregateId: projectId, aggregateType: 'Project', eventType: 'AccessRevoked', projectId, version: 1 };
}
