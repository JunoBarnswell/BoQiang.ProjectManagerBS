import { useEffect, useState } from 'react';

import type { ProjectManagementMyWorkItem, ProjectManagementTaskUpsertRequest } from '../../../api/project-management/projectManagement.types';
import { PermissionButton } from '../../../shared/auth/PermissionButton';

interface MyWorkTaskCommandPanelProps {
  item: ProjectManagementMyWorkItem | null;
  onCancel: () => void;
  onSubmit: (request: ProjectManagementTaskUpsertRequest) => void;
  saving: boolean;
}

function toRequest(item: ProjectManagementMyWorkItem): ProjectManagementTaskUpsertRequest {
  const task = item.task;
  return {
    assigneeEmploymentId: task.assigneeEmploymentId,
    assigneeUserId: task.assigneeUserId,
    description: task.description,
    dueDate: task.dueDate,
    estimateMinutes: task.estimateMinutes,
    milestoneId: task.milestoneId,
    parentTaskId: task.parentTaskId,
    priority: task.priority,
    progressPercent: task.progressPercent,
    startDate: task.startDate,
    status: task.status,
    taskCode: task.taskCode,
    title: task.title,
    versionNo: task.versionNo,
    weight: task.weight,
  };
}

export function MyWorkTaskCommandPanel({ item, onCancel, onSubmit, saving }: MyWorkTaskCommandPanelProps) {
  const [request, setRequest] = useState<ProjectManagementTaskUpsertRequest | null>(null);

  useEffect(() => setRequest(item ? toRequest(item) : null), [item]);
  if (!item || !request) return null;

  return (
    <section className="mb-4 rounded-lg border border-gray-200 p-4">
      <div className="mb-3 font-semibold">快速更新：{item.task.title}</div>
      <div className="grid gap-3 md:grid-cols-4">
        <label className="text-sm">状态<select className="mt-1 w-full rounded border border-gray-300 p-2" onChange={(event) => setRequest({ ...request, status: event.target.value })} value={request.status ?? 'Todo'}>{['Todo', 'InProgress', 'Blocked', 'Completed', 'Cancelled'].map((status) => <option key={status}>{status}</option>)}</select></label>
        <label className="text-sm">进度<input className="mt-1 w-full rounded border border-gray-300 p-2" max={100} min={0} onChange={(event) => setRequest({ ...request, progressPercent: Number(event.target.value) })} type="number" value={request.progressPercent ?? 0} /></label>
        <label className="text-sm">负责人用户 ID<input className="mt-1 w-full rounded border border-gray-300 p-2" onChange={(event) => setRequest({ ...request, assigneeUserId: event.target.value.trim() || undefined, assigneeEmploymentId: undefined })} value={request.assigneeUserId ?? ''} /></label>
        <label className="text-sm">优先级<select className="mt-1 w-full rounded border border-gray-300 p-2" onChange={(event) => setRequest({ ...request, priority: event.target.value })} value={request.priority ?? 'Medium'}>{['Low', 'Medium', 'High', 'Urgent'].map((priority) => <option key={priority}>{priority}</option>)}</select></label>
      </div>
      <div className="mt-3 flex gap-2"><PermissionButton code="project-management:task:edit" disabled={saving} onClick={() => onSubmit(request)}>{saving ? '保存中…' : '保存任务'}</PermissionButton><button onClick={onCancel} type="button">取消</button></div>
    </section>
  );
}
