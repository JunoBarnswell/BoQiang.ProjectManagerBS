import { useState } from 'react';

import {
  createProjectManagementProjectLabel,
  createProjectManagementPublicLabel,
  deleteProjectManagementProjectLabel,
  deleteProjectManagementPublicLabel,
  updateProjectManagementProjectLabel,
  updateProjectManagementPublicLabel,
} from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementLabel, ProjectManagementTaskLabelFilter } from '../../../api/project-management/projectManagement.types';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../shared/utils/errorMessage';

interface TaskWorkspaceLabelManagerProps {
  filter: ProjectManagementTaskLabelFilter;
  labels: ProjectManagementLabel[];
  onChanged: () => Promise<void>;
  onFilterChange: (filter: ProjectManagementTaskLabelFilter) => void;
  projectId: string;
}

export function TaskWorkspaceLabelManager({ filter, labels, onChanged, onFilterChange, projectId }: TaskWorkspaceLabelManagerProps) {
  const confirm = useConfirm();
  const message = useMessage();
  const [labelName, setLabelName] = useState('');
  const [color, setColor] = useState('#64748B');
  const [scope, setScope] = useState<'Project' | 'Public'>('Project');
  const createMutation = useApiMutation({
    mutationFn: () => scope === 'Public'
      ? createProjectManagementPublicLabel({ labelName, color })
      : createProjectManagementProjectLabel(projectId, { labelName, color }),
    onError: (error) => message.error(getErrorMessage(error, '标签创建失败')),
    onSuccess: async () => {
      message.success('标签已创建');
      setLabelName('');
      await onChanged();
    },
  });
  const updateMutation = useApiMutation({
    mutationFn: (label: ProjectManagementLabel) => label.scope === 'Public'
      ? updateProjectManagementPublicLabel(label.id, { labelName: label.labelName, color: label.color, versionNo: label.versionNo })
      : updateProjectManagementProjectLabel(projectId, label.id, { labelName: label.labelName, color: label.color, versionNo: label.versionNo }),
    onError: (error) => message.error(getErrorMessage(error, '标签颜色更新失败')),
    onSuccess: async () => {
      message.success('标签颜色已更新');
      await onChanged();
    },
  });
  const deleteMutation = useApiMutation({
    mutationFn: async (label: ProjectManagementLabel) => ({
      labelId: label.id,
      result: label.scope === 'Public'
        ? await deleteProjectManagementPublicLabel(label.id, label.versionNo)
        : await deleteProjectManagementProjectLabel(projectId, label.id, label.versionNo),
    }),
    onError: (error) => message.error(getErrorMessage(error, '标签删除失败')),
    onSuccess: async ({ labelId }) => {
      message.success('标签已删除，关联关系已解除');
      onFilterChange({ ...filter, labelIds: filter.labelIds.filter((id) => id !== labelId) });
      await onChanged();
    },
  });

  return (
    <section className="rounded-lg border p-4" aria-label="标签管理" style={{ background: 'var(--pm-surface)', borderColor: 'var(--pm-border)', color: 'var(--pm-text)' }}>
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div><h2 className="font-semibold">标签管理</h2><p className="text-sm" style={{ color: 'var(--pm-muted)' }}>公共标签可用于当前工作区的所有项目；删除标签只解除任务关联，不删除任务。</p></div>
      </div>
      <div className="grid gap-2 md:grid-cols-4">
        <select aria-label="标签范围" value={scope} onChange={(event) => setScope(event.target.value as 'Project' | 'Public')}>
          <option value="Project">项目标签</option><option value="Public">公共标签</option>
        </select>
        <input aria-label="标签名称" value={labelName} onChange={(event) => setLabelName(event.target.value)} placeholder="标签名称" />
        <input aria-label="标签颜色" type="color" value={color} onChange={(event) => setColor(event.target.value.toUpperCase())} />
        <PermissionButton code="project-management:label:manage" disabled={!labelName.trim() || createMutation.isPending} onClick={() => createMutation.mutate()}>{createMutation.isPending ? '创建中…' : '创建标签'}</PermissionButton>
      </div>
      <div className="mt-3 flex flex-wrap gap-2">
        {labels.map((label) => (
          <div className="flex items-center gap-2 rounded border px-2 py-1 text-sm" key={label.id} style={{ borderColor: 'var(--pm-border)', background: 'var(--pm-surface-subtle)' }}>
            <input aria-label={`更新 ${label.labelName} 颜色`} type="color" value={label.color} onChange={(event) => updateMutation.mutate({ ...label, color: event.target.value.toUpperCase() })} />
            <span className="rounded px-2 py-0.5 font-medium" style={{ background: label.color, color: contrastText(label.color) }}>{label.labelName}</span><span className="text-xs" style={{ color: 'var(--pm-muted)' }}>{label.scope === 'Public' ? '公共' : '项目'}</span>
            <PermissionButton code="project-management:label:manage" disabled={deleteMutation.isPending} onClick={() => confirm({ title: '删除标签', content: `将删除“${label.labelName}”并解除其全部任务关联，任务本身不会被删除。`, confirmText: '删除标签', onConfirm: () => deleteMutation.mutate(label) })}>删除</PermissionButton>
          </div>
        ))}
      </div>
      <fieldset className="mt-4 grid gap-2 rounded border p-3 text-sm" style={{ borderColor: 'var(--pm-border)', background: 'var(--pm-surface-subtle)' }}>
        <legend className="px-1 font-medium">筛选当前视图</legend>
        <select aria-label="标签筛选匹配模式" value={filter.matchMode ?? 'Any'} onChange={(event) => onFilterChange({ ...filter, matchMode: event.target.value as 'Any' | 'All' })}>
          <option value="Any">匹配任一标签</option>
          <option value="All">匹配全部标签</option>
        </select>
        <div className="flex flex-wrap gap-2">
          {labels.map((label) => <label className="flex items-center gap-1 rounded border px-2 py-1" key={`filter-${label.id}`} style={{ borderColor: 'var(--pm-border)' }}><input checked={filter.labelIds.includes(label.id)} type="checkbox" onChange={(event) => onFilterChange({ ...filter, labelIds: event.target.checked ? [...filter.labelIds, label.id] : filter.labelIds.filter((labelId) => labelId !== label.id) })} /><span className="rounded px-1.5 py-0.5" style={{ background: label.color, color: contrastText(label.color) }}>{label.labelName}</span></label>)}
        </div>
      </fieldset>
    </section>
  );
}

function contrastText(color: string): string {
  const normalized = /^#[0-9a-fA-F]{6}$/.test(color) ? color.slice(1) : '64748B';
  const red = Number.parseInt(normalized.slice(0, 2), 16);
  const green = Number.parseInt(normalized.slice(2, 4), 16);
  const blue = Number.parseInt(normalized.slice(4, 6), 16);
  return (red * 299 + green * 587 + blue * 114) / 1000 >= 150 ? '#111827' : '#F8FAFC';
}
