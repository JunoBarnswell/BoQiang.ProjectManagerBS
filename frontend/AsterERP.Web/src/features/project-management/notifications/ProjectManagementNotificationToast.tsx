import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import {
  getProjectManagementNotifications,
  openProjectManagementNotification,
} from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementNotification } from '../../../api/project-management/projectManagement.types';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { useMessage } from '../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { useProjectManagementI18n } from '../projectManagementI18n';
import { normalizeProjectManagementTargetRoute } from '../state/projectManagementPlatformRoutes';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

import { isHighValueProjectManagementNotification } from './useProjectManagementBrowserNotifications';
import { useProjectManagementNotificationRealtime } from './useProjectManagementNotificationRealtime';

import './projectManagementNotificationEntry.css';

const popupEligibleTypes = new Set([
  'task.comment.mentioned',
  'task.participant.added',
  'task.participant.removed',
  'task.assigned',
]);

/**
 * A foreground-only companion to browser notifications. The durable PM
 * notification is still opened through the server-side `/open` authorization
 * endpoint, so the toast never trusts a route supplied by the client.
 */
export function ProjectManagementNotificationToast() {
  const { format, t } = useProjectManagementI18n();
  const scope = useProjectManagementWorkspaceScope();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const message = useMessage();
  const seen = useRef(new Set<string>());
  const initialized = useRef(false);
  const [active, setActive] = useState<ProjectManagementNotification>();
  const notificationQuery = { pageIndex: 1, pageSize: 20, unreadOnly: true };
  const queryKey = projectManagementQueryKeys.notifications(scope, notificationQuery);
  const notifications = useQuery({
    enabled: scope.isAvailable,
    queryFn: ({ signal }) => getProjectManagementNotifications(notificationQuery, signal),
    queryKey,
  });
  const refresh = useCallback(
    () => queryClient.invalidateQueries({ queryKey }),
    [queryClient, queryKey],
  );
  useProjectManagementNotificationRealtime('/hubs/system-notification', scope, scope.isAvailable, refresh);

  useEffect(() => {
    const items = notifications.data?.data.items ?? [];
    if (!initialized.current) {
      items.forEach((item) => seen.current.add(item.id));
      initialized.current = true;
      return;
    }
    const next = items.find((item) =>
      !seen.current.has(item.id) &&
      isHighValueProjectManagementNotification(item.notificationType) &&
      popupEligibleTypes.has(item.notificationType));
    items.forEach((item) => seen.current.add(item.id));
    if (next) setActive(next);
  }, [notifications.data?.data.items]);

  const open = async () => {
    if (!active) return;
    try {
      const result = await openProjectManagementNotification(active.id);
      const target = result.data;
      if (!target?.isAvailable || !target.targetRoute) {
        message.error(target?.unavailableReason ?? t('projectManagement.notification.unavailable'));
        return;
      }
      setActive(undefined);
      await refresh();
      navigate(normalizeProjectManagementTargetRoute(target.targetRoute));
    } catch (error) {
      message.error(getErrorMessage(error, t('projectManagement.notification.openFailed')));
    }
  };

  if (!active) return null;
  const title = active.titleText ? format(active.titleText.key, active.titleText.arguments) : active.title;
  const body = active.messageText ? format(active.messageText.key, active.messageText.arguments) : active.message;
  return (
    <aside aria-live="assertive" className="pm-notify-toast" role="alert">
      <button aria-label={t('common.close')} className="pm-notify-toast__close" onClick={() => setActive(undefined)} type="button">×</button>
      <strong>{title}</strong>
      <p>{body}</p>
      <button className="pm-notify-toast__action" onClick={() => void open()} type="button">
        {t('projectManagement.notification.viewTask')}
      </button>
    </aside>
  );
}
