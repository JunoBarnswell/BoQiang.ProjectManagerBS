import { useState } from 'react';

import type {
  ProjectManagementMember,
  ProjectManagementTaskReminder,
  ProjectManagementTaskReminderCreateRequest,
} from '../../../api/project-management/projectManagement.types';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { PermissionGuard } from '../../../shared/auth/PermissionGuard';

interface TaskWorkspaceReminderPanelProps {
  creating: boolean;
  error: boolean;
  loading: boolean;
  members: ProjectManagementMember[];
  onCancel: (reminder: ProjectManagementTaskReminder) => void;
  onCreate: (request: ProjectManagementTaskReminderCreateRequest) => void;
  onDelete: (reminder: ProjectManagementTaskReminder) => void;
  reminders: ProjectManagementTaskReminder[];
}

const localDateTime = (value: Date) => {
  const offset = value.getTimezoneOffset() * 60_000;
  return new Date(value.getTime() - offset).toISOString().slice(0, 16);
};

const newClientRequestId = () => globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`;

export function TaskWorkspaceReminderPanel({
  creating,
  error,
  loading,
  members,
  onCancel,
  onCreate,
  onDelete,
  reminders,
}: TaskWorkspaceReminderPanelProps) {
  const [reminderAt, setReminderAt] = useState(() => localDateTime(new Date(Date.now() + 60 * 60_000)));
  const [scope, setScope] = useState<ProjectManagementTaskReminderCreateRequest['recipientScope']>('Self');
  const [memberIds, setMemberIds] = useState<string[]>([]);
  const [note, setNote] = useState('');

  const submit = () => {
    if (!reminderAt) return;
    onCreate({
      reminderAt: new Date(reminderAt).toISOString(),
      timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC',
      recipientScope: scope,
      recipientUserIds: scope === 'Members' ? memberIds : undefined,
      note: note.trim() || undefined,
      clientRequestId: newClientRequestId(),
    });
  };

  return (
    <section>
      <div className="mb-2 font-semibold">任务提醒</div>
      {loading ? <div className="text-sm text-gray-500">正在加载提醒…</div> : null}
      {error ? <div className="mb-2 rounded bg-amber-50 p-2 text-sm text-amber-800">提醒加载失败，请重新选择任务后重试。</div> : null}
      {!loading && !error && reminders.length === 0 ? <div className="mb-2 text-sm text-gray-500">暂无提醒</div> : null}
      {!loading && !error && reminders.length > 0 ? <div className="mb-3 space-y-2">{reminders.map((reminder) => (
        <article className="rounded border border-gray-100 p-2" key={reminder.id}>
          <div className="flex items-center justify-between gap-2 text-sm"><span>{new Date(reminder.reminderAtUtc).toLocaleString()} · {reminder.recipientUserId}</span><span className="text-xs text-gray-500">{reminder.status}</span></div>
          {reminder.note ? <div className="mt-1 text-sm text-gray-700">{reminder.note}</div> : null}
          {reminder.lastError ? <div className="mt-1 text-xs text-red-600">{reminder.lastError}</div> : null}
          <PermissionGuard code="project-management:reminder:manage" fallback={null}>
            <div className="mt-2 flex gap-2">
              {reminder.status === 'Pending' ? <button className="text-sm text-amber-700 underline" onClick={() => onCancel(reminder)} type="button">取消</button> : null}
              {reminder.status !== 'Pending' ? <button className="text-sm text-red-700 underline" onClick={() => onDelete(reminder)} type="button">删除记录</button> : null}
            </div>
          </PermissionGuard>
        </article>
      ))}</div> : null}
      <PermissionGuard code="project-management:reminder:manage" fallback={<div className="text-sm text-gray-500">你没有管理提醒的权限。</div>}>
        <div className="grid gap-2 md:grid-cols-2">
          <input aria-label="提醒时间" min={localDateTime(new Date())} onChange={(event) => setReminderAt(event.target.value)} type="datetime-local" value={reminderAt} />
          <select aria-label="提醒对象" onChange={(event) => setScope(event.target.value as ProjectManagementTaskReminderCreateRequest['recipientScope'])} value={scope}>
            <option value="Self">提醒自己</option><option value="Assignee">提醒负责人</option><option value="Participants">提醒参与人</option><option value="Members">指定成员</option>
          </select>
          {scope === 'Members' ? <select aria-label="指定提醒成员" multiple onChange={(event) => setMemberIds(Array.from(event.currentTarget.selectedOptions, (option) => option.value))} value={memberIds}>{members.map((member) => <option key={member.id} value={member.userId}>{member.userId} · {member.roleCode}</option>)}</select> : null}
          <input aria-label="提醒备注" maxLength={1000} onChange={(event) => setNote(event.target.value)} placeholder="提醒备注（可选）" value={note} />
        </div>
        <div className="mt-2"><PermissionButton code="project-management:reminder:manage" disabled={creating || !reminderAt || (scope === 'Members' && memberIds.length === 0)} onClick={submit}>{creating ? '创建中…' : '新增提醒'}</PermissionButton></div>
      </PermissionGuard>
    </section>
  );
}
