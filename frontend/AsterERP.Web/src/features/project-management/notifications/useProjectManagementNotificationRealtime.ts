import { useEffect } from 'react';

import { acquireProjectManagementHubConnection } from '../hooks/projectManagementHubConnection';
import type { ProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

interface ProjectManagementNotificationCreatedEvent {
  notificationId: string;
}

export function useProjectManagementNotificationRealtime(
  signalRUrl: string,
  scope: ProjectManagementWorkspaceScope,
  enabled: boolean,
  onNotificationCreated: () => void,
) {
  useEffect(() => {
    if (!enabled || !scope.isAvailable) return undefined;
    const connection = acquireProjectManagementHubConnection(signalRUrl, scope);
    if (!connection) return undefined;
    const notify = (event: ProjectManagementNotificationCreatedEvent) => {
      if (!event.notificationId) return;
      onNotificationCreated();
    };
    connection.subscribe('ProjectManagementNotificationCreated', notify);
    connection.subscribeLifecycle({
      connected: onNotificationCreated,
      reconnected: onNotificationCreated,
    });
    return () => {
      connection.dispose();
    };
  }, [enabled, onNotificationCreated, scope, signalRUrl]);
}
