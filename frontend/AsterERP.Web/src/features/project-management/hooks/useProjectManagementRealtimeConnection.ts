import { useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';

import { queryKeys } from '../../../core/query/queryKeys';
import type { ProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

import { acquireProjectManagementHubConnection } from './projectManagementHubConnection';

interface ProjectManagementInvalidation {
  aggregateType: 'Project' | 'ProjectMember' | 'Task' | 'TaskAttachment' | 'TaskComment' | 'TaskReminder';
  aggregateId: string;
  eventType: string;
  projectId: string;
  version: number;
}

interface ProjectManagementAccessRevoked {
  projectId: string;
}

export function useProjectManagementRealtimeConnection(
  signalRUrl: string,
  scope: ProjectManagementWorkspaceScope,
  projectId: string,
  enabled: boolean,
  onReconciled?: () => void,
) {
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!enabled || !scope.isAvailable || !projectId) return undefined;
    let disposed = false;
    let flushTimer: ReturnType<typeof setTimeout> | undefined;
    const pendingAggregateTypes = new Set<ProjectManagementInvalidation['aggregateType']>();
    const versionWatermarks = new Map<string, number>();
    const connection = acquireProjectManagementHubConnection(signalRUrl, scope);
    if (!connection) return undefined;
    const flushInvalidations = () => {
      flushTimer = undefined;
      const aggregateTypes = new Set(pendingAggregateTypes);
      pendingAggregateTypes.clear();
      if (aggregateTypes.has('Project') || aggregateTypes.has('ProjectMember') || aggregateTypes.has('Task')) {
        void queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.tasksProject(scope, projectId) });
        void queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.overview(scope, { projectId }) });
      }
      if (aggregateTypes.has('TaskComment')) {
        void queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.taskCommentsProject(scope, projectId) });
      }
      if (aggregateTypes.has('TaskAttachment')) {
        void queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.taskAttachmentsProject(scope, projectId) });
      }
    };
    const reconcile = () => {
      versionWatermarks.clear();
      pendingAggregateTypes.add('Project');
      pendingAggregateTypes.add('Task');
      pendingAggregateTypes.add('TaskComment');
      pendingAggregateTypes.add('TaskAttachment');
      onReconciled?.();
      flushInvalidations();
    };
    const invalidate = (event: ProjectManagementInvalidation) => {
      if (event.projectId !== projectId || event.version <= 0) return;
      const versionKey = `${event.aggregateType}:${event.aggregateId}`;
      if ((versionWatermarks.get(versionKey) ?? 0) >= event.version) return;
      versionWatermarks.set(versionKey, event.version);
      pendingAggregateTypes.add(event.aggregateType);
      if (!flushTimer) flushTimer = setTimeout(flushInvalidations, 120);
    };
    const revokeAccess = (event: ProjectManagementAccessRevoked) => {
      if (event.projectId !== projectId) return;
      versionWatermarks.clear();
      leaveProject();
      reconcile();
    };
    connection.subscribe('ProjectManagementInvalidated', invalidate);
    connection.subscribe('ProjectManagementAccessRevoked', revokeAccess);
    const leaveProject = connection.subscribeProject(projectId, () => {
      if (!disposed) reconcile();
    });
    return () => {
      disposed = true;
      if (flushTimer) clearTimeout(flushTimer);
      connection.dispose();
    };
  }, [enabled, onReconciled, projectId, queryClient, scope, signalRUrl]);
}
