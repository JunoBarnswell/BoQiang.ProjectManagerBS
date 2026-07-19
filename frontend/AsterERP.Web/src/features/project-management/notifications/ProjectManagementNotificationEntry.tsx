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
import { normalizeProjectManagementTargetRoute } from '../state/projectManagementPlatformRoutes';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

import { useProjectManagementBrowserNotifications } from './useProjectManagementBrowserNotifications';
import { useProjectManagementNotificationRealtime } from './useProjectManagementNotificationRealtime';

const notificationTypeOptions = [
  { label: '全部类型', value: '' },
  { label: '任务提醒', value: 'task.reminder' },
  { label: '@ 提及', value: 'task.comment.mentioned' },
  { label: '任务分配', value: 'task.assigned' },
  { label: '加入参与人', value: 'task.participant.added' },
  { label: '移出参与人', value: 'task.participant.removed' },
  { label: '任务状态', value: 'task.status.changed' },
  { label: '截止日期', value: 'task.due-date.changed' },
  { label: '里程碑风险', value: 'milestone.risk.detected' },
  { label: 'Excel 导入', value: 'project.excel-import' },
  { label: '同步导入', value: 'sync.import' },
  { label: '操作完成', value: 'operation.succeeded' },
  { label: '操作失败', value: 'operation.failed' },
] as const;

export function ProjectManagementNotificationEntry() {
  return (
    <PermissionGuard code="project-management:notification:view" fallback={null}>
      <ProjectManagementNotificationEntryContent />
    </PermissionGuard>
  );
}

function ProjectManagementNotificationEntryContent() {
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
        message.error(target?.unavailableReason ?? '关联对象不可访问');
        return;
      }
      setOpen(false);
      navigate(normalizeProjectManagementTargetRoute(target.targetRoute));
    },
    [message, navigate, refresh],
  );
  const markAll = useApiMutation({
    mutationFn: markAllProjectManagementNotificationsRead,
    onError: (error) => message.error(getErrorMessage(error, '全部标记已读失败')),
    onSuccess: () => { void refresh(); },
  });
  const markRead = useApiMutation({
    mutationFn: (item: ProjectManagementNotification) => markProjectManagementNotificationRead(item.id),
    onError: (error) => message.error(getErrorMessage(error, '标记已读失败')),
    onSuccess: () => { void refresh(); },
  });
  const openNotification = useApiMutation({
    mutationFn: (item: ProjectManagementNotification) => openNotificationById(item.id),
    onError: (error) => message.error(getErrorMessage(error, '通知打开失败')),
  });
  const page = notifications.data?.data;
  const browser = useProjectManagementBrowserNotifications({
    notifications: page?.items ?? [],
    onOpen: (notificationId) => { void openNotificationById(notificationId).catch((error: unknown) => message.error(getErrorMessage(error, '通知打开失败'))); },
    scope,
    userId,
  });
  const realtimeState = useProjectManagementNotificationRealtime('/hubs/system-notification', scope, scope.isAvailable, refresh);

  if (!scope.isAvailable) return null;
  const unread = page?.unreadCount ?? 0;
  return (
    <div className="relative hidden sm:block">
      <button aria-expanded={open} className="relative cursor-pointer transition-colors hover:text-white" onClick={() => setOpen((value) => !value)} title="项目通知" type="button">
        <AppIcon className="text-lg" name="bell" />
        {unread > 0 ? <span className="absolute -right-2 -top-2 min-w-4 rounded-full bg-red-500 px-1 text-[10px] font-semibold leading-4 text-white">{unread > 99 ? '99+' : unread}</span> : null}
      </button>
      {open ? (
        <div className="absolute right-0 top-8 z-50 w-96 rounded border border-gray-200 bg-white p-3 text-gray-800 shadow-xl">
          <div className="mb-2 flex items-center justify-between">
            <strong>项目通知</strong>
            <div className="flex items-center gap-2 text-xs">
              <button className={!unreadOnly ? 'font-semibold text-blue-700' : ''} onClick={() => setUnreadOnly(false)} type="button">全部</button>
              <button className={unreadOnly ? 'font-semibold text-blue-700' : ''} onClick={() => setUnreadOnly(true)} type="button">未读</button>
              <select aria-label="通知类型" className="max-w-24 rounded border border-gray-300 bg-white px-1 py-0.5" onChange={(event) => setNotificationType(event.target.value)} value={notificationType}>
                {notificationTypeOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
              </select>
              <button disabled={markAll.isPending || unread === 0} onClick={() => markAll.mutate()} type="button">全部已读</button>
            </div>
          </div>
          {realtimeState !== 'connected' ? <p className="mb-2 text-xs text-amber-700">实时连接暂不可用，通知仍会保存在站内；连接恢复后自动刷新。</p> : null}
          {browser.canRequestPermission ? <button className="mb-2 text-sm text-blue-700 underline" onClick={() => { void browser.requestPermission(); }} type="button">启用浏览器通知</button> : null}
          {browser.supported && browser.permission === 'denied' ? <p className="mb-2 text-xs text-gray-500">浏览器通知已被拒绝，将仅显示站内通知。</p> : null}
          {notifications.isLoading ? <div className="py-4 text-sm text-gray-500">正在加载通知…</div> : null}
          {notifications.isError ? <div className="py-4 text-sm text-red-600">通知加载失败。<button className="ml-2 underline" onClick={() => void notifications.refetch()} type="button">重试</button></div> : null}
          {!notifications.isLoading && !notifications.isError && (page?.items.length ?? 0) === 0 ? <div className="py-4 text-sm text-gray-500">暂无项目通知</div> : null}
          {!notifications.isLoading && !notifications.isError && (page?.items.length ?? 0) > 0 ? <div className="max-h-96 space-y-2 overflow-y-auto">{page?.items.map((item) => <div className={`rounded border p-2 text-sm ${item.isRead ? 'border-gray-100' : 'border-blue-200 bg-blue-50'}`} key={item.id}><button className="block w-full text-left hover:bg-gray-50" disabled={openNotification.isPending} onClick={() => openNotification.mutate(item)} type="button"><div className="font-medium">{item.title}</div><div className="mt-1 text-gray-600">{item.message}</div><div className="mt-1 text-xs text-gray-400">{new Date(item.createdTime).toLocaleString()}</div></button>{!item.isRead ? <button className="mt-1 text-xs text-blue-700 underline" disabled={markRead.isPending} onClick={() => markRead.mutate(item)} type="button">标记已读</button> : null}</div>)}</div> : null}
        </div>
      ) : null}
    </div>
  );
}
