import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';

import type { ProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';
import {
  getProjectManagementViewSyncInvalidationTargets,
  isProjectManagementViewSyncInvalidation,
  shouldClearProjectManagementViewSyncSelection,
  type ProjectManagementViewSyncInvalidation,
  type ProjectManagementViewSyncInvalidationTarget,
} from '../view-sync/projectManagementViewSyncModel';

import { acquireProjectManagementHubConnection } from './projectManagementHubConnection';
import { applyProjectManagementTaskPatch, invalidateProjectManagementViewSyncTargets } from './useProjectManagementViewSync';

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
 * filters the subscribed project again, rejects stale aggregate versions, and batches cache invalidations.
 */
export function useProjectManagementViewSyncRealtime({ enabled, onReconciled, onSelectedTaskDeleted, projectId, selectedTaskId, signalRUrl, scope }: ProjectManagementViewSyncRealtimeOptions): void {
  const queryClient = useQueryClient();
  const selectedTaskIdRef = useRef(selectedTaskId);
  selectedTaskIdRef.current = selectedTaskId;

  useEffect(() => {
    if (!enabled || !scope.isAvailable || !projectId) return undefined;
    let flushTimer: ReturnType<typeof setTimeout> | undefined;
    const versions = new Map<string, number>();
    const pendingTargets = new Set<ProjectManagementViewSyncInvalidationTarget>();
    const connection = acquireProjectManagementHubConnection(signalRUrl, scope);
    if (!connection) return undefined;

    const flushInvalidations = () => {
      flushTimer = undefined;
      const targets = new Set(pendingTargets);
      pendingTargets.clear();
      if (targets.size > 0) void invalidateProjectManagementViewSyncTargets(queryClient, { projectId, scope }, targets);
    };
    const queueInvalidation = (event: ProjectManagementViewSyncInvalidation, immediate = false, skipTargets?: ReadonlySet<ProjectManagementViewSyncInvalidationTarget>) => {
      for (const target of getProjectManagementViewSyncInvalidationTargets(event)) if (!skipTargets?.has(target)) pendingTargets.add(target);
      if (immediate) {
        if (flushTimer) clearTimeout(flushTimer);
        flushInvalidations();
      } else if (!flushTimer) {
        flushTimer = setTimeout(flushInvalidations, 120);
      }
    };
    const reconcile = () => {
      versions.clear();
      onReconciled?.();
      queueInvalidation({ aggregateId: projectId, aggregateType: 'Project', eventType: 'Reconciled', projectId, version: 1 }, true);
      queueInvalidation({ aggregateId: projectId, aggregateType: 'TaskComment', eventType: 'Reconciled', projectId, version: 1 });
      queueInvalidation({ aggregateId: projectId, aggregateType: 'TaskAttachment', eventType: 'Reconciled', projectId, version: 1 });
      queueInvalidation({ aggregateId: projectId, aggregateType: 'TaskReminder', eventType: 'Reconciled', projectId, version: 1 });
    };

    const unsubscribeInvalidation = connection.subscribe('ProjectManagementInvalidated', (payload: unknown) => {
      if (!isProjectManagementViewSyncInvalidation(payload) || payload.projectId !== projectId || payload.version <= 0) return;
      const versionKey = `${payload.aggregateType}:${payload.aggregateId}`;
      if ((versions.get(versionKey) ?? 0) >= payload.version) return;
      versions.set(versionKey, payload.version);
      if (shouldClearProjectManagementViewSyncSelection(payload, selectedTaskIdRef.current)) onSelectedTaskDeleted?.(payload.aggregateId);
      const patchApplied = applyProjectManagementTaskPatch(queryClient, { projectId, scope }, payload);
      queueInvalidation(payload, false, patchApplied ? new Set<ProjectManagementViewSyncInvalidationTarget>(['tasks']) : undefined);
    });
    const unsubscribeRevocation = connection.subscribe('ProjectManagementAccessRevoked', (payload: unknown) => {
      const event = readProjectAccessRevocation(payload);
      if (!event || event.projectId !== projectId) return;
      if (selectedTaskIdRef.current) onSelectedTaskDeleted?.(selectedTaskIdRef.current);
      reconcile();
    });
    const leaveProject = connection.subscribeProject(projectId, () => {
      reconcile();
    });
    return () => {
      unsubscribeInvalidation();
      unsubscribeRevocation();
      leaveProject();
      if (flushTimer) clearTimeout(flushTimer);
      connection.dispose();
    };
  }, [enabled, onReconciled, onSelectedTaskDeleted, projectId, queryClient, scope, signalRUrl]);
}

function readProjectAccessRevocation(payload: unknown): ProjectManagementViewSyncInvalidation | undefined {
  if (!payload || typeof payload !== 'object' || typeof (payload as { projectId?: unknown }).projectId !== 'string') return undefined;
  const projectId = (payload as { projectId: string }).projectId;
  return { aggregateId: projectId, aggregateType: 'Project', eventType: 'AccessRevoked', projectId, version: 1 };
}
