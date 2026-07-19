import { useEffect, useState } from 'react';

import { acquireProjectManagementHubConnection, type ProjectManagementHubConnectionState } from '../hooks/projectManagementHubConnection';
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
  const [connectionState, setConnectionState] = useState<ProjectManagementHubConnectionState>('disconnected');

  useEffect(() => {
    if (!enabled || !scope.isAvailable) {
      setConnectionState('disconnected');
      return undefined;
    }
    const connection = acquireProjectManagementHubConnection(signalRUrl, scope);
    if (!connection) {
      setConnectionState('disconnected');
      return undefined;
    }
    const notify = (event: ProjectManagementNotificationCreatedEvent) => {
      if (!event.notificationId) return;
      onNotificationCreated();
    };
    connection.subscribe('ProjectManagementNotificationCreated', notify);
    connection.subscribeLifecycle({
      connected: onNotificationCreated,
      reconnected: onNotificationCreated,
      stateChanged: setConnectionState,
    });
    return () => {
      connection.dispose();
    };
  }, [enabled, onNotificationCreated, scope, signalRUrl]);

  return connectionState;
}
