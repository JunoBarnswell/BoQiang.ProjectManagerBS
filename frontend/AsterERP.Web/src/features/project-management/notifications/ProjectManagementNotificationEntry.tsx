import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import {
  getProjectManagementNotifications,
  markProjectManagementNotificationRead,
  markAllProjectManagementNotificationsRead,
  openProjectManagementNotification,
} from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementNotification } from '../../../api/project-management/projectManagement.types';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useAuthStore } from '../../../core/state';
import { PermissionGuard } from '../../../shared/auth/PermissionGuard';
import { useMessage } from '../../../shared/feedback/useMessage';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { useProjectManagementI18n } from '../projectManagementI18n';
import { normalizeProjectManagementTargetRoute } from '../state/projectManagementPlatformRoutes';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

import { useProjectManagementBrowserNotifications } from './useProjectManagementBrowserNotifications';
import { useProjectManagementNotificationRealtime } from './useProjectManagementNotificationRealtime';

const notificationTypeOptions = [
  ['', 'projectManagement.notification.type.all'], ['task.reminder', 'projectManagement.notification.type.taskReminder'], ['task.comment.mentioned', 'projectManagement.notification.type.mentioned'], ['task.assigned', 'projectManagement.notification.type.assigned'], ['task.participant.added', 'projectManagement.notification.type.participantAdded'], ['task.participant.removed', 'projectManagement.notification.type.participantRemoved'], ['task.status.changed', 'projectManagement.notification.type.statusChanged'], ['task.due-date.changed', 'projectManagement.notification.type.dueDateChanged'], ['milestone.risk.detected', 'projectManagement.notification.type.milestoneRisk'], ['project.excel-import', 'projectManagement.notification.type.excelImport'], ['sync.export', 'projectManagement.notification.type.syncExport'], ['sync.import', 'projectManagement.notification.type.syncImport'], ['operation.succeeded', 'projectManagement.notification.type.succeeded'], ['operation.failed', 'projectManagement.notification.type.failed'],
] as const;

export function ProjectManagementNotificationEntry() {
  return (
    <PermissionGuard code="project-management:notification:view" fallback={null}>
      <ProjectManagementNotificationEntryContent />
    </PermissionGuard>
  );
}

function ProjectManagementNotificationEntryContent() {
  const { dateTime, format, t } = useProjectManagementI18n();
  const navigate = useNavigate();
  const scope = useProjectManagementWorkspaceScope();
  const userId = useAuthStore((state) => state.user?.userId ?? '');
  const queryClient = useQueryClient();
  const message = useMessage();
  const [open, setOpen] = useState(false);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [notificationType, setNotificationType] = useState('');
  const notificationQuery = useMemo(() => ({ pageIndex: 1, pageSize: 20, unreadOnly, ...(notificationType ? { notificationType } : {}) }), [notificationType, unreadOnly]);
  const notifications = useQuery({
    enabled: scope.isAvailable,
    queryFn: ({ signal }) => getProjectManagementNotifications(notificationQuery, signal),
    queryKey: projectManagementQueryKeys.notifications(scope, notificationQuery),
  });
  const refresh = useCallback(
    () => queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.notifications(scope, notificationQuery) }),
    [notificationQuery, queryClient, scope],
  );
  const openNotificationById = useCallback(
    async (notificationId: string) => {
      const result = await openProjectManagementNotification(notificationId);
      await refresh();
      const target = result.data;
      if (!target?.isAvailable || !target.targetRoute) {
        message.error(target?.unavailableReasonText ? format(target.unavailableReasonText.key, target.unavailableReasonText.arguments) : target?.unavailableReason ?? t('projectManagement.notification.unavailable'));
        return;
      }
      setOpen(false);
      navigate(normalizeProjectManagementTargetRoute(target.targetRoute));
    },
    [format, message, navigate, refresh, t],
  );
  const markAll = useApiMutation({
    mutationFn: markAllProjectManagementNotificationsRead,
    onError: (error) => message.error(getErrorMessage(error, t('projectManagement.notification.markAllFailed'))),
    onSuccess: () => { void refresh(); },
  });
  const markRead = useApiMutation({
    mutationFn: (item: ProjectManagementNotification) => markProjectManagementNotificationRead(item.id),
    onError: (error) => message.error(getErrorMessage(error, t('projectManagement.notification.markReadFailed'))),
    onSuccess: () => { void refresh(); },
  });
  const openNotification = useApiMutation({
    mutationFn: (item: ProjectManagementNotification) => openNotificationById(item.id),
    onError: (error) => message.error(getErrorMessage(error, t('projectManagement.notification.openFailed'))),
  });
  const page = notifications.data?.data;
  const browser = useProjectManagementBrowserNotifications({
    notifications: page?.items ?? [],
    onOpen: (notificationId) => { void openNotificationById(notificationId).catch((error: unknown) => message.error(getErrorMessage(error, t('projectManagement.notification.openFailed')))); },
    scope,
    userId,
  });
  const realtimeState = useProjectManagementNotificationRealtime('/hubs/system-notification', scope, scope.isAvailable, refresh);

  if (!scope.isAvailable) return null;
  const unread = page?.unreadCount ?? 0;
  return (
    <div className="relative hidden sm:block">
      <button aria-expanded={open} className="relative cursor-pointer transition-colors hover:text-white" onClick={() => setOpen((value) => !value)} title={t('projectManagement.notification.title')} type="button">
        <AppIcon className="text-lg" name="bell" />
        {unread > 0 ? <span className="absolute -right-2 -top-2 min-w-4 rounded-full bg-red-500 px-1 text-[10px] font-semibold leading-4 text-white">{unread > 99 ? t('projectManagement.notification.countOverflow') : unread}</span> : null}
      </button>
      {open ? (
        <div className="absolute right-0 top-8 z-50 w-96 rounded border border-gray-200 bg-white p-3 text-gray-800 shadow-xl">
          <div className="mb-2 flex items-center justify-between">
            <strong>{t('projectManagement.notification.title')}</strong>
            <div className="flex items-center gap-2 text-xs">
              <button className={!unreadOnly ? 'font-semibold text-blue-700' : ''} onClick={() => setUnreadOnly(false)} type="button">{t('projectManagement.notification.all')}</button>
              <button className={unreadOnly ? 'font-semibold text-blue-700' : ''} onClick={() => setUnreadOnly(true)} type="button">{t('projectManagement.notification.unread')}</button>
              <select aria-label={t('projectManagement.notification.typeAria')} className="max-w-24 rounded border border-gray-300 bg-white px-1 py-0.5" onChange={(event) => setNotificationType(event.target.value)} value={notificationType}>
                {notificationTypeOptions.map(([value, key]) => <option key={value} value={value}>{t(key)}</option>)}
              </select>
              <button disabled={markAll.isPending || unread === 0} onClick={() => markAll.mutate()} type="button">{t('projectManagement.notification.markAllRead')}</button>
            </div>
          </div>
          {realtimeState !== 'connected' ? <p className="mb-2 text-xs text-amber-700">{t('projectManagement.notification.realtimeUnavailable')}</p> : null}
          {browser.canRequestPermission ? <button className="mb-2 text-sm text-blue-700 underline" onClick={() => { void browser.requestPermission(); }} type="button">{t('projectManagement.notification.enableBrowser')}</button> : null}
          {browser.supported && browser.permission === 'denied' ? <p className="mb-2 text-xs text-gray-500">{t('projectManagement.notification.browserDenied')}</p> : null}
          {notifications.isLoading ? <div className="py-4 text-sm text-gray-500">{t('projectManagement.notification.loading')}</div> : null}
          {notifications.isError ? <div className="py-4 text-sm text-red-600">{t('projectManagement.notification.loadFailed')}<button className="ml-2 underline" onClick={() => void notifications.refetch()} type="button">{t('projectManagement.operation.retry')}</button></div> : null}
          {!notifications.isLoading && !notifications.isError && (page?.items.length ?? 0) === 0 ? <div className="py-4 text-sm text-gray-500">{t('projectManagement.notification.empty')}</div> : null}
          {!notifications.isLoading && !notifications.isError && (page?.items.length ?? 0) > 0 ? <div className="max-h-96 space-y-2 overflow-y-auto">{page?.items.map((item) => <div className={`rounded border p-2 text-sm ${item.isRead ? 'border-gray-100' : 'border-blue-200 bg-blue-50'}`} key={item.id}><button className="block w-full text-left hover:bg-gray-50" disabled={openNotification.isPending} onClick={() => openNotification.mutate(item)} type="button"><div className="font-medium">{item.titleText ? format(item.titleText.key, item.titleText.arguments) : item.title}</div><div className="mt-1 text-gray-600">{item.messageText ? format(item.messageText.key, item.messageText.arguments) : item.message}</div><div className="mt-1 text-xs text-gray-400">{dateTime(item.createdTime)}</div></button>{!item.isRead ? <button className="mt-1 text-xs text-blue-700 underline" disabled={markRead.isPending} onClick={() => markRead.mutate(item)} type="button">{t('projectManagement.notification.markRead')}</button> : null}</div>)}</div> : null}
        </div>
      ) : null}
    </div>
  );
}
