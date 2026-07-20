import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, type CSSProperties } from 'react';
import { createPortal } from 'react-dom';
import { useNavigate } from 'react-router-dom';

import {
  getProjectManagementNotifications,
  markAllProjectManagementNotificationsRead,
  markProjectManagementNotificationRead,
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

import './projectManagementNotificationEntry.css';

const PANEL_WIDTH = 400;
const PANEL_GAP = 8;
const VIEWPORT_PADDING = 8;

const notificationTypeOptions = [
  ['', 'projectManagement.notification.type.all'],
  ['task.reminder', 'projectManagement.notification.type.taskReminder'],
  ['task.comment.mentioned', 'projectManagement.notification.type.mentioned'],
  ['task.assigned', 'projectManagement.notification.type.assigned'],
  ['task.participant.added', 'projectManagement.notification.type.participantAdded'],
  ['task.participant.removed', 'projectManagement.notification.type.participantRemoved'],
  ['task.status.changed', 'projectManagement.notification.type.statusChanged'],
  ['task.due-date.changed', 'projectManagement.notification.type.dueDateChanged'],
  ['milestone.risk.detected', 'projectManagement.notification.type.milestoneRisk'],
  ['project.excel-import', 'projectManagement.notification.type.excelImport'],
  ['sync.export', 'projectManagement.notification.type.syncExport'],
  ['sync.import', 'projectManagement.notification.type.syncImport'],
  ['operation.succeeded', 'projectManagement.notification.type.succeeded'],
  ['operation.failed', 'projectManagement.notification.type.failed'],
] as const;

const notificationToneByType: Record<string, string> = {
  'task.reminder': 'is-reminder',
  'task.comment.mentioned': 'is-mention',
  'task.assigned': 'is-assign',
  'task.participant.added': 'is-assign',
  'task.participant.removed': 'is-muted',
  'task.status.changed': 'is-status',
  'task.due-date.changed': 'is-due',
  'milestone.risk.detected': 'is-risk',
  'project.excel-import': 'is-sync',
  'sync.export': 'is-sync',
  'sync.import': 'is-sync',
  'operation.succeeded': 'is-success',
  'operation.failed': 'is-danger',
};

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
  const triggerRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [panelStyle, setPanelStyle] = useState<CSSProperties>({});
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [notificationType, setNotificationType] = useState('');
  const notificationQuery = useMemo(
    () => ({ pageIndex: 1, pageSize: 20, unreadOnly, ...(notificationType ? { notificationType } : {}) }),
    [notificationType, unreadOnly],
  );
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
        message.error(
          target?.unavailableReasonText
            ? format(target.unavailableReasonText.key, target.unavailableReasonText.arguments)
            : target?.unavailableReason ?? t('projectManagement.notification.unavailable'),
        );
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
    onOpen: (notificationId) => {
      void openNotificationById(notificationId).catch((error: unknown) =>
        message.error(getErrorMessage(error, t('projectManagement.notification.openFailed'))),
      );
    },
    scope,
    userId,
  });
  const realtimeState = useProjectManagementNotificationRealtime('/hubs/system-notification', scope, scope.isAvailable, refresh);

  const updatePanelPosition = useCallback(() => {
    const trigger = triggerRef.current;
    if (!trigger) return;
    const rect = trigger.getBoundingClientRect();
    const width = Math.min(PANEL_WIDTH, window.innerWidth - VIEWPORT_PADDING * 2);
    const preferredRight = window.innerWidth - rect.right;
    const right = Math.max(VIEWPORT_PADDING, Math.min(preferredRight, window.innerWidth - VIEWPORT_PADDING - width));
    const top = Math.min(rect.bottom + PANEL_GAP, window.innerHeight - VIEWPORT_PADDING - 120);
    setPanelStyle({
      position: 'fixed',
      top,
      right,
      width,
      maxHeight: Math.max(240, window.innerHeight - top - VIEWPORT_PADDING),
    });
  }, []);

  useLayoutEffect(() => {
    if (!open) return undefined;
    updatePanelPosition();
    window.addEventListener('resize', updatePanelPosition);
    window.addEventListener('scroll', updatePanelPosition, true);
    return () => {
      window.removeEventListener('resize', updatePanelPosition);
      window.removeEventListener('scroll', updatePanelPosition, true);
    };
  }, [open, updatePanelPosition]);

  useEffect(() => {
    if (!open) return undefined;
    const onPointerDown = (event: MouseEvent) => {
      const target = event.target as Node;
      if (triggerRef.current?.contains(target) || panelRef.current?.contains(target)) return;
      setOpen(false);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };
    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  if (!scope.isAvailable) return null;
  const unread = page?.unreadCount ?? 0;
  const items = page?.items ?? [];
  const typeLabel = (type: string) => {
    const matched = notificationTypeOptions.find(([value]) => value === type);
    return matched ? t(matched[1]) : type;
  };

  const panel = open
    ? createPortal(
      <div
        aria-label={t('projectManagement.notification.title')}
        className="pm-notify__panel"
        ref={panelRef}
        role="dialog"
        style={panelStyle}
      >
        <div className="pm-notify__head">
          <div className="pm-notify__title-wrap">
            <strong className="pm-notify__title">{t('projectManagement.notification.title')}</strong>
            {unread > 0 ? <span className="pm-notify__count">{unread}</span> : null}
          </div>
          <button aria-label={t('common.close')} className="pm-notify__close" onClick={() => setOpen(false)} type="button">
            <AppIcon name="x" />
          </button>
        </div>

        <div className="pm-notify__toolbar">
          <div className="pm-notify__tabs" role="tablist">
            <button
              aria-selected={!unreadOnly}
              className={!unreadOnly ? 'is-active' : undefined}
              onClick={() => setUnreadOnly(false)}
              role="tab"
              type="button"
            >
              {t('projectManagement.notification.all')}
            </button>
            <button
              aria-selected={unreadOnly}
              className={unreadOnly ? 'is-active' : undefined}
              onClick={() => setUnreadOnly(true)}
              role="tab"
              type="button"
            >
              {t('projectManagement.notification.unread')}
              {unread > 0 ? <span>{unread}</span> : null}
            </button>
          </div>
          <select
            aria-label={t('projectManagement.notification.typeAria')}
            className="pm-notify__type"
            onChange={(event) => setNotificationType(event.target.value)}
            value={notificationType}
          >
            {notificationTypeOptions.map(([value, key]) => (
              <option key={value || 'all'} value={value}>{t(key)}</option>
            ))}
          </select>
          <button
            className="pm-notify__mark-all"
            disabled={markAll.isPending || unread === 0}
            onClick={() => markAll.mutate()}
            type="button"
          >
            {t('projectManagement.notification.markAllRead')}
          </button>
        </div>

        {realtimeState !== 'connected' ? (
          <p className="pm-notify__alert is-warning">{t('projectManagement.notification.realtimeUnavailable')}</p>
        ) : null}
        {browser.canRequestPermission ? (
          <button className="pm-notify__browser" onClick={() => { void browser.requestPermission(); }} type="button">
            {t('projectManagement.notification.enableBrowser')}
          </button>
        ) : null}
        {browser.supported && browser.permission === 'denied' ? (
          <p className="pm-notify__alert is-muted">{t('projectManagement.notification.browserDenied')}</p>
        ) : null}

        <div className="pm-notify__body">
          {notifications.isLoading ? <div className="pm-notify__state">{t('projectManagement.notification.loading')}</div> : null}
          {notifications.isError ? (
            <div className="pm-notify__state is-error">
              <span>{t('projectManagement.notification.loadFailed')}</span>
              <button onClick={() => void notifications.refetch()} type="button">{t('projectManagement.operation.retry')}</button>
            </div>
          ) : null}
          {!notifications.isLoading && !notifications.isError && items.length === 0 ? (
            <div className="pm-notify__empty">
              <AppIcon name="inbox" />
              <strong>{t('projectManagement.notification.empty')}</strong>
            </div>
          ) : null}
          {!notifications.isLoading && !notifications.isError && items.length > 0 ? (
            <ul className="pm-notify__list">
              {items.map((item) => {
                const title = item.titleText ? format(item.titleText.key, item.titleText.arguments) : item.title;
                const body = item.messageText ? format(item.messageText.key, item.messageText.arguments) : item.message;
                const tone = notificationToneByType[item.notificationType] ?? 'is-muted';
                return (
                  <li className={`pm-notify__item${item.isRead ? '' : ' is-unread'}`} key={item.id}>
                    <button
                      className="pm-notify__item-main"
                      disabled={openNotification.isPending}
                      onClick={() => openNotification.mutate(item)}
                      type="button"
                    >
                      <span aria-hidden className={`pm-notify__dot ${tone}`} />
                      <span className="pm-notify__item-content">
                        <span className="pm-notify__item-meta">
                          <span className={`pm-notify__chip ${tone}`}>{typeLabel(item.notificationType)}</span>
                          <time dateTime={item.createdTime}>{dateTime(item.createdTime)}</time>
                        </span>
                        <span className="pm-notify__item-title">{title}</span>
                        {body ? <span className="pm-notify__item-message">{body}</span> : null}
                      </span>
                    </button>
                    {!item.isRead ? (
                      <button
                        className="pm-notify__mark-read"
                        disabled={markRead.isPending}
                        onClick={() => markRead.mutate(item)}
                        type="button"
                      >
                        {t('projectManagement.notification.markRead')}
                      </button>
                    ) : null}
                  </li>
                );
              })}
            </ul>
          ) : null}
        </div>
      </div>,
      document.body,
    )
    : null;

  return (
    <div className="pm-notify hidden sm:block">
      <button
        aria-expanded={open}
        aria-haspopup="dialog"
        className={`pm-notify__trigger${open ? ' is-open' : ''}`}
        data-pm-notification-entry
        onClick={() => setOpen((value) => !value)}
        ref={triggerRef}
        title={t('projectManagement.notification.title')}
        type="button"
      >
        <AppIcon className="text-lg" name="bell" />
        {unread > 0 ? (
          <span className="pm-notify__badge">
            {unread > 99 ? t('projectManagement.notification.countOverflow') : unread}
          </span>
        ) : null}
      </button>
      {panel}
    </div>
  );
}
