import { useEffect, useMemo, useRef, useState } from 'react';

import type { ProjectManagementNotification } from '../../../api/project-management/projectManagement.types';
import type { ProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

const highValueNotificationTypes = new Set([
  'task.reminder.sent',
  'task.comment.mentioned',
  'task.assigned',
  'task.participant.changed',
  'task.schedule.changed',
  'milestone.at-risk',
  'sync.failed',
  'operation.failed',
]);

interface BrowserNotificationOptions {
  notifications: readonly ProjectManagementNotification[];
  onOpen: (notificationId: string) => void;
  scope: ProjectManagementWorkspaceScope;
  userId: string;
}

function isSupported() {
  return typeof window !== 'undefined' && 'Notification' in window;
}

function storageKey(scope: ProjectManagementWorkspaceScope, userId: string) {
  return `astererp.pm.browser-notifications:${scope.tenantId}:${scope.appCode}:${userId}`;
}

function readSeen(key: string): Set<string> {
  try {
    return new Set(JSON.parse(window.localStorage.getItem(key) ?? '[]').filter((value: unknown) => typeof value === 'string').slice(-300));
  } catch {
    return new Set();
  }
}

function writeSeen(key: string, seen: Set<string>) {
  try {
    window.localStorage.setItem(key, JSON.stringify([...seen].slice(-300)));
  } catch {
    // Hardened browser contexts still retain station notifications without system delivery.
  }
}

export function useProjectManagementBrowserNotifications({ notifications, onOpen, scope, userId }: BrowserNotificationOptions) {
  const supported = isSupported();
  const [permission, setPermission] = useState<NotificationPermission | 'unsupported'>(() => supported ? Notification.permission : 'unsupported');
  const initialized = useRef(false);
  const onOpenRef = useRef(onOpen);
  const seen = useRef<Set<string>>(new Set());
  const key = useMemo(() => scope.isAvailable && userId ? storageKey(scope, userId) : '', [scope, userId]);

  onOpenRef.current = onOpen;

  useEffect(() => {
    initialized.current = false;
    seen.current = key ? readSeen(key) : new Set();
  }, [key]);

  useEffect(() => {
    if (!key) return undefined;
    const receive = (notificationId: string) => {
      if (!notificationId) return;
      seen.current.add(notificationId);
      writeSeen(key, seen.current);
    };
    const storageListener = (event: StorageEvent) => {
      if (event.key !== key) return;
      readSeen(key).forEach(receive);
    };
    window.addEventListener('storage', storageListener);
    if (!supported || typeof BroadcastChannel === 'undefined') return () => window.removeEventListener('storage', storageListener);
    const channel = new BroadcastChannel(key);
    channel.onmessage = (event: MessageEvent<string>) => receive(typeof event.data === 'string' ? event.data : '');
    return () => {
      window.removeEventListener('storage', storageListener);
      channel.close();
    };
  }, [key, supported]);

  useEffect(() => {
    if (!key || !notifications.length) return;
    if (!initialized.current) {
      notifications.forEach((item) => seen.current.add(item.id));
      writeSeen(key, seen.current);
      initialized.current = true;
      return;
    }
    const channel = supported && typeof BroadcastChannel !== 'undefined' ? new BroadcastChannel(key) : undefined;
    notifications.filter((item) => !item.isRead && highValueNotificationTypes.has(item.notificationType) && !seen.current.has(item.id)).forEach((item) => {
      seen.current.add(item.id);
      channel?.postMessage(item.id);
      if (permission === 'granted' && document.visibilityState !== 'visible') {
        const notification = new Notification(item.title, { body: item.message, tag: `pm-notification:${item.id}` });
        notification.onclick = () => {
          window.focus();
          onOpenRef.current(item.id);
        };
      }
    });
    channel?.close();
    writeSeen(key, seen.current);
  }, [key, notifications, permission, supported]);

  const requestPermission = async () => {
    if (!supported || Notification.permission !== 'default') return;
    setPermission(await Notification.requestPermission());
  };

  return { canRequestPermission: permission === 'default', permission, requestPermission, supported };
}
