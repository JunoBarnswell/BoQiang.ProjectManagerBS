import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';

import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { acquireProjectManagementHubConnection, type ProjectManagementHubConnectionState } from '../hooks/projectManagementHubConnection';
import { applyProjectManagementTaskPatch } from '../hooks/useProjectManagementViewSync';
import type { ProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';
import type { ProjectManagementViewSyncInvalidation } from '../view-sync/projectManagementViewSyncModel';

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
  patch?: Record<string, unknown>;
  traceId?: string;
}

export function useProjectManagementProjectRealtime({ signalRUrl, scope, projectId, enabled, onAccessRevoked }: { signalRUrl: string; scope: ProjectManagementWorkspaceScope; projectId: string; enabled: boolean; onAccessRevoked?: () => void }) {
  const queryClient = useQueryClient();
  const [connectionState, setConnectionState] = useState<ProjectManagementHubConnectionState>('disconnected');
  const onAccessRevokedRef = useRef(onAccessRevoked);
  onAccessRevokedRef.current = onAccessRevoked;

  const tenantId = scope.tenantId;
  const appCode = scope.appCode;
  const userId = scope.userId;
  const isAvailable = scope.isAvailable;

  useEffect(() => {
    if (!enabled || !isAvailable || !projectId) return undefined;
    const scopedScope: ProjectManagementWorkspaceScope = { tenantId, appCode, userId, isAvailable };
    const connection = acquireProjectManagementHubConnection(signalRUrl, scopedScope);
    if (!connection) return undefined;
    let timer: ReturnType<typeof setTimeout> | undefined;
    let disposed = false;
    let latestProjectSequence = 0;
    const aggregateVersions = new Map<string, number>();
    const flush = () => {
      timer = undefined;
      if (disposed) return;
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.overview(scopedScope, { projectId, pageIndex: 1, pageSize: 1 }) });
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.milestones(scopedScope, projectId) });
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.activities(scopedScope, projectId, { pageIndex: 1, pageSize: 20 }) });
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.resources(scopedScope, projectId) });
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.tasksProject(scopedScope, projectId) });
    };
    const queueFlush = () => {
      if (!timer) timer = setTimeout(flush, 120);
    };
    const reconcile = () => {
      latestProjectSequence = 0;
      aggregateVersions.clear();
      queueFlush();
    };
    const unsubscribeInvalidation = connection.subscribe('ProjectManagementInvalidated', (payload: unknown) => {
      const event = readEvent(payload);
      if (!event || event.projectId !== projectId || (event.tenantId && event.tenantId !== tenantId) || (event.appCode && event.appCode.toUpperCase() !== appCode.toUpperCase())) return;
      const sequence = event.projectSequence ?? event.aggregateVersion ?? 0;
      const key = `${event.aggregateType ?? ''}:${event.aggregateId ?? ''}`;
      if (sequence > 0 && sequence < latestProjectSequence) return;
      if (sequence > 0) latestProjectSequence = Math.max(latestProjectSequence, sequence);
      const aggregateVersion = event.aggregateVersion ?? 0;
      if (aggregateVersion > 0 && (aggregateVersions.get(key) ?? 0) >= aggregateVersion) return;
      if (aggregateVersion > 0) aggregateVersions.set(key, aggregateVersion);
      const patchApplied = applyProjectManagementTaskPatch(queryClient, { projectId, scope: scopedScope }, {
        aggregateId: event.aggregateId ?? '',
        aggregateType: event.aggregateType === 'Task' ? 'Task' : event.aggregateType === 'TaskAttachment' ? 'TaskAttachment' : event.aggregateType === 'TaskComment' ? 'TaskComment' : event.aggregateType === 'TaskReminder' ? 'TaskReminder' : event.aggregateType === 'ProjectMember' ? 'ProjectMember' : 'Project',
        changedFields: event.changedFields,
        eventType: event.eventType ?? 'updated',
        patch: event.patch,
        projectId,
        version: event.aggregateVersion ?? 0,
      } satisfies ProjectManagementViewSyncInvalidation);
      if (!patchApplied) queueFlush();
    });
    const unsubscribeRevocation = connection.subscribe('ProjectManagementAccessRevoked', (payload: unknown) => {
      const event = readEvent(payload);
      if (event?.projectId === projectId) onAccessRevokedRef.current?.();
    });
    // Do not reconcile on mere "connected" registration — that fires every effect remount while the hub is already up.
    const unsubscribeLifecycle = connection.subscribeLifecycle({ reconnected: reconcile, stateChanged: setConnectionState });
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
  }, [appCode, enabled, isAvailable, projectId, queryClient, signalRUrl, tenantId, userId]);

  return { connectionState };
}

function readEvent(payload: unknown): ProjectInvalidationEvent | undefined {
  if (!payload || typeof payload !== 'object') return undefined;
  const event = payload as ProjectInvalidationEvent;
  if (typeof event.projectId !== 'string' || !event.projectId) return undefined;
  return event;
}
