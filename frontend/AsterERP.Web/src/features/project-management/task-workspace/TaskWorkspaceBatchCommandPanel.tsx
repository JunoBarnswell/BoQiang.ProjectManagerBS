import { useMemo, useState } from 'react';

import type {
  ProjectManagementLabel,
  ProjectManagementMemberCandidate,
  ProjectManagementMilestone,
  ProjectManagementTask,
  ProjectManagementTaskBatchUpdateRequest,
} from '../../../api/project-management/projectManagement.types';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { ResponsiveModal } from '../../../shared/responsive/ResponsiveModal';

interface TaskWorkspaceBatchCommandPanelProps {
  candidates: ProjectManagementMemberCandidate[];
  candidatesError: boolean;
  labels: ProjectManagementLabel[];
  labelsError: boolean;
  milestones: ProjectManagementMilestone[];
  milestonesError: boolean;
  onClose: () => void;
  onSubmit: (request: ProjectManagementTaskBatchUpdateRequest) => void;
  open: boolean;
  pending: boolean;
  projectId: string;
  tasks: ProjectManagementTask[];
}

interface BatchFormState {
  assigneeUserId: string;
  dueDate: string;
  labelIds: string[];
  milestoneId: string;
  priority: string;
  startDate: string;
  status: string;
  updateLabels: boolean;
  updateMilestone: boolean;
  updateSchedule: boolean;
}

const initialForm: BatchFormState = {
  assigneeUserId: '',
  dueDate: '',
  labelIds: [],
  milestoneId: '',
  priority: '',
  startDate: '',
  status: '',
  updateLabels: false,
  updateMilestone: false,
  updateSchedule: false,
};

export function TaskWorkspaceBatchCommandPanel({
  candidates,
  candidatesError,
  labels,
  labelsError,
  milestones,
  milestonesError,
  onClose,
  onSubmit,
  open,
  pending,
  projectId,
  tasks,
}: TaskWorkspaceBatchCommandPanelProps) {
  const [form, setForm] = useState<BatchFormState>(initialForm);
  const hasChanges = useMemo(() => Boolean(
    form.status || form.priority || form.assigneeUserId || form.updateMilestone || form.updateSchedule || form.updateLabels,
  ), [form]);

  const close = () => {
    if (pending) return;
    setForm(initialForm);
    onClose();
  };
  const submit = () => {
    if (!hasChanges || pending || tasks.length === 0) return;
    onSubmit({
      projectId,
      items: tasks.map((task) => ({ taskId: task.id, versionNo: task.versionNo })),
      status: form.status || undefined,
      priority: form.priority || undefined,
      assigneeUserId: form.assigneeUserId === '__clear__' ? '' : form.assigneeUserId || undefined,
      milestoneId: form.updateMilestone ? (form.milestoneId === '__clear__' ? '' : form.milestoneId || undefined) : undefined,
      updateMilestone: form.updateMilestone,
      startDate: form.updateSchedule ? form.startDate || undefined : undefined,
      dueDate: form.updateSchedule ? form.dueDate || undefined : undefined,
      updateSchedule: form.updateSchedule,
      labelIds: form.updateLabels ? form.labelIds : undefined,
      updateLabels: form.updateLabels,
    });
  };
  const toggleLabel = (labelId: string, checked: boolean) => {
    setForm((current) => ({
      ...current,
      labelIds: checked ? [...current.labelIds, labelId] : current.labelIds.filter((id) => id !== labelId),
    }));
  };

  return (
    <ResponsiveModal
      description={`将对 ${tasks.length} 个任务执行同一原子命令；任一版本、权限、WIP、依赖或数据校验失败时不会产生部分更新。`}
      footer={<><button type="button" disabled={pending} onClick={close}>取消</button><PermissionButton code="project-management:task:edit" disabled={!hasChanges || pending || tasks.length === 0} onClick={submit}>{pending ? '批量更新中…' : `更新 ${tasks.length} 个任务`}</PermissionButton></>}
      onClose={close}
      open={open}
      title="批量更新任务"
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <label className="grid gap-1 text-sm">状态<select value={form.status} onChange={(event) => setForm((current) => ({ ...current, status: event.target.value }))}><option value="">不变</option>{['Todo', 'InProgress', 'Blocked', 'Done', 'Cancelled'].map((status) => <option key={status} value={status}>{status}</option>)}</select></label>
        <label className="grid gap-1 text-sm">优先级<select value={form.priority} onChange={(event) => setForm((current) => ({ ...current, priority: event.target.value }))}><option value="">不变</option>{['Low', 'Medium', 'High', 'Urgent'].map((priority) => <option key={priority} value={priority}>{priority}</option>)}</select></label>
        <label className="grid gap-1 text-sm">负责人<select value={form.assigneeUserId} onChange={(event) => setForm((current) => ({ ...current, assigneeUserId: event.target.value }))}><option value="">不变</option><option value="__clear__">清空负责人</option>{candidates.map((candidate) => <option key={`${candidate.userId}-${candidate.employmentId}`} value={candidate.userId}>{candidate.displayName} · {candidate.employmentName}</option>)}</select>{candidatesError ? <span className="text-xs text-amber-700">候选人员加载失败，暂不能从列表选择负责人。</span> : null}</label>
        <label className="grid gap-1 text-sm"><span><input checked={form.updateMilestone} type="checkbox" onChange={(event) => setForm((current) => ({ ...current, updateMilestone: event.target.checked }))} /> 更新里程碑</span><select disabled={!form.updateMilestone} value={form.milestoneId} onChange={(event) => setForm((current) => ({ ...current, milestoneId: event.target.value }))}><option value="">请选择里程碑</option><option value="__clear__">清空里程碑</option>{milestones.map((milestone) => <option key={milestone.id} value={milestone.id}>{milestone.milestoneName}</option>)}</select>{milestonesError ? <span className="text-xs text-amber-700">里程碑加载失败，暂不能更新此字段。</span> : null}</label>
        <label className="grid gap-1 text-sm sm:col-span-2"><span><input checked={form.updateSchedule} type="checkbox" onChange={(event) => setForm((current) => ({ ...current, updateSchedule: event.target.checked }))} /> 更新计划日期</span><span className="grid gap-2 sm:grid-cols-2"><input aria-label="批量开始日期" disabled={!form.updateSchedule} type="date" value={form.startDate} onChange={(event) => setForm((current) => ({ ...current, startDate: event.target.value }))} /><input aria-label="批量截止日期" disabled={!form.updateSchedule} type="date" value={form.dueDate} onChange={(event) => setForm((current) => ({ ...current, dueDate: event.target.value }))} /></span></label>
        <fieldset className="grid gap-2 text-sm sm:col-span-2"><label><input checked={form.updateLabels} type="checkbox" onChange={(event) => setForm((current) => ({ ...current, updateLabels: event.target.checked }))} /> 替换标签</label>{labelsError ? <span className="text-xs text-amber-700">标签加载失败，暂不能更新标签。</span> : <div className="flex flex-wrap gap-2">{labels.map((label) => <label className="rounded border border-gray-200 px-2 py-1" key={label.id}><input checked={form.labelIds.includes(label.id)} disabled={!form.updateLabels} type="checkbox" onChange={(event) => toggleLabel(label.id, event.target.checked)} /> <span style={{ color: label.color }}>{label.labelName}</span></label>)}</div>}</fieldset>
      </div>
    </ResponsiveModal>
  );
}
