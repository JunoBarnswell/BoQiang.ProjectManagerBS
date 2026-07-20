import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';

import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { acquireProjectManagementHubConnection } from '../hooks/projectManagementHubConnection';
import type { ProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

interface HomeInvalidation {
  tenantId?: string;
  appCode?: string;
  aggregateType?: string;
  aggregateId?: string;
  eventType?: string;
  projectId?: string;
  version?: number;
  sequence?: number;
  aggregateVersion?: number;
  workspaceSequence?: number;
  projectSequence?: number;
  changedFields?: string[];
  patch?: Record<string, unknown>;
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
    const projectVersions = new Map<string, number>();
    const patchProjectRows = (event: HomeInvalidation, projectId: string) => {
      const patch = event.patch;
      const fields = event.changedFields ?? Object.keys(patch ?? {});
      const safeFields = fields.every(field => ['projectName', 'projectCode', 'priority', 'versionNo', 'updatedTime'].includes(field));
      if (!patch || !safeFields) return false;
      let patched = false;
      queryClient.setQueriesData<unknown>({ queryKey: projectManagementQueryKeys.homeProjectsRoot(scope) }, (current: unknown) => {
        if (!current || typeof current !== 'object') return current;
        const envelope = current as { data?: { items?: Array<{ id: string; [key: string]: unknown }> } };
        if (!envelope.data?.items) return current;
        const items = envelope.data.items.map(item => item.id === projectId ? (patched = true, { ...item, ...patch }) : item);
        return patched ? { ...envelope, data: { ...envelope.data, items } } : current;
      });
      return patched;
    };
    const removeProjectRow = (projectId: string) => {
      queryClient.setQueriesData<unknown>({ queryKey: projectManagementQueryKeys.homeProjectsRoot(scope) }, (current: unknown) => {
        if (!current || typeof current !== 'object') return current;
        const envelope = current as { data?: { items?: Array<{ id: string; [key: string]: unknown }>; total?: number } };
        if (!envelope.data?.items) return current;
        const items = envelope.data.items.filter(item => item.id !== projectId);
        return items.length === envelope.data.items.length ? current : { ...envelope, data: { ...envelope.data, items, total: Math.max(0, (envelope.data.total ?? items.length) - 1) } };
      });
    };
    const flush = () => {
      timer = undefined;
      if (disposed) return;
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.homeProjectsRoot(scope) });
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.homeSummaryRoot(scope) });
    };
    const onInvalidated = (event: HomeInvalidation) => {
      if (!event || (event.tenantId && event.tenantId !== scope.tenantId) || (event.appCode && event.appCode.toUpperCase() !== scope.appCode.toUpperCase())) return;
      const version = event.aggregateVersion ?? event.version ?? 0;
      const sequence = event.workspaceSequence ?? event.sequence ?? event.projectSequence ?? version;
      if (version <= 0 && sequence <= 0) return;
      if (sequence < newestSequence) return;
      const projectId = event.projectId ?? event.aggregateId;
      if (projectId && version > 0 && (projectVersions.get(projectId) ?? 0) >= version) return;
      if (projectId && version > 0) projectVersions.set(projectId, version);
      newestSequence = Math.max(newestSequence, sequence);
      if (event.aggregateType === 'Project' && projectId && event.patch?.isDeleted === true) removeProjectRow(projectId);
      if (event.aggregateType === 'Project' && projectId && patchProjectRows(event, projectId)) return;
      if (!timer) timer = setTimeout(flush, 120);
    };
    const reconcile = () => {
      newestSequence = 0;
      projectVersions.clear();
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
