import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { useEffect } from 'react';

import { getAccessToken } from '../../../core/http/tokenStorage';
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
    let disposed = false;
    const connection = new HubConnectionBuilder()
      .withUrl(signalRUrl, { accessTokenFactory: () => getAccessToken() })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    const notify = (event: ProjectManagementNotificationCreatedEvent) => {
      if (!event.notificationId) return;
      onNotificationCreated();
    };
    connection.on('ProjectManagementNotificationCreated', notify);
    connection.onreconnected(() => onNotificationCreated());
    void (async () => {
      try {
        await connection.start();
        if (!disposed && connection.state === HubConnectionState.Connected) onNotificationCreated();
      } catch {
        // The station notification list remains available via its existing query.
      }
    })();
    return () => {
      disposed = true;
      connection.off('ProjectManagementNotificationCreated', notify);
      void connection.stop();
    };
  }, [enabled, onNotificationCreated, scope, signalRUrl]);
}
