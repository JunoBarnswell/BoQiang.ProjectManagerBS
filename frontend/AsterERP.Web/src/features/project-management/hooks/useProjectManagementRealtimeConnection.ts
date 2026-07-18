import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';

import { getAccessToken } from '@/core/http/tokenStorage';

import { queryKeys } from '../../../core/query/queryKeys';
import type { ProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

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
) {
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!enabled || !scope.isAvailable || !projectId) return undefined;
    let disposed = false;
    let flushTimer: ReturnType<typeof setTimeout> | undefined;
    const pendingAggregateTypes = new Set<ProjectManagementInvalidation['aggregateType']>();
    const versionWatermarks = new Map<string, number>();
    const connection = new HubConnectionBuilder()
      .withUrl(signalRUrl, { accessTokenFactory: () => getAccessToken() })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
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
      void connection.invoke('LeaveProjectManagementProject', projectId).catch(() => undefined);
      reconcile();
    };
    const joinAndReconcile = async () => {
      if (disposed || connection.state !== HubConnectionState.Connected) return;
      await connection.invoke('JoinProjectManagementProject', projectId);
      reconcile();
    };
    connection.on('ProjectManagementInvalidated', invalidate);
    connection.on('ProjectManagementAccessRevoked', revokeAccess);
    connection.onreconnected(() => joinAndReconcile().catch(() => undefined));
    void (async () => {
      await Promise.resolve();
      if (disposed) return;
      try {
        await connection.start();
        await joinAndReconcile();
      } catch {
        // Automatic reconnect remains enabled; the page continues to use the last consistent query snapshot.
      }
    })();
    return () => {
      disposed = true;
      if (flushTimer) clearTimeout(flushTimer);
      connection.off('ProjectManagementInvalidated', invalidate);
      connection.off('ProjectManagementAccessRevoked', revokeAccess);
      void connection.invoke('LeaveProjectManagementProject', projectId).catch(() => undefined).finally(() => connection.stop());
    };
  }, [enabled, projectId, queryClient, scope, signalRUrl]);
}
