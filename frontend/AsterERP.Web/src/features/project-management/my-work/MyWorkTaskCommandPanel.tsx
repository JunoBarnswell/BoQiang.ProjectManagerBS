import { useQuery } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';

import { getProjectManagementMemberCandidates } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementMyWorkItem, ProjectManagementTaskUpsertRequest } from '../../../api/project-management/projectManagement.types';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { priorityLabel, taskStatusLabel } from '../projectManagementPresentation';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';
import { ModalForm } from '../../../shared/forms/ModalForm';
import type { FormFieldConfig } from '../../../shared/forms/formTypes';

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
  const scope = useProjectManagementWorkspaceScope();
  const [request, setRequest] = useState<ProjectManagementTaskUpsertRequest | null>(null);
  const candidatesQuery = useQuery({
    enabled: scope.isAvailable && Boolean(item),
    queryKey: projectManagementQueryKeys.memberCandidates(scope, { pageIndex: 1, pageSize: 100, projectId: item?.task.projectId ?? '' }),
    queryFn: ({ signal }) => getProjectManagementMemberCandidates({ pageIndex: 1, pageSize: 100, projectId: item?.task.projectId }, signal),
  });
  useEffect(() => setRequest(item ? toRequest(item) : null), [item]);

  const candidates = candidatesQuery.data?.data.items ?? [];
  const assigneeOptions = useMemo(() => candidates.map((candidate) => ({ label: `${candidate.displayName || candidate.userName} · ${candidate.employmentName}`, value: candidate.userId })), [candidates]);
  const fields: FormFieldConfig<ProjectManagementTaskUpsertRequest>[] = [
    { label: '状态', name: 'status', options: ['Todo', 'InProgress', 'Blocked', 'Done', 'Cancelled'].map((status) => ({ label: taskStatusLabel(status), value: status })), section: '快速更新', type: 'select' },
    { label: '优先级', name: 'priority', options: ['Low', 'Medium', 'High', 'Urgent'].map((priority) => ({ label: priorityLabel(priority), value: priority })), section: '快速更新', type: 'select' },
    { label: '完成进度', name: 'progressPercent', max: 100, min: 0, section: '快速更新', span: 2, step: 1, type: 'range' },
    { label: '负责人', name: 'assigneeUserId', options: assigneeOptions, emptyOptionLabel: candidatesQuery.isLoading ? '正在加载项目成员…' : '选择负责人', section: '快速更新', span: 2, type: 'select' },
  ];

  if (!item || !request) return null;
  return (
    <ModalForm
      actions={[
        { label: '取消', onClick: onCancel, variant: 'ghost' },
        { label: '保存任务', loading: saving, onClick: () => onSubmit(request), variant: 'primary' },
      ]}
      fields={fields}
      open={Boolean(item)}
      onClose={onCancel}
      onValueChange={(name, value) => {
        if (name === 'assigneeUserId') {
          const candidate = candidates.find((item) => item.userId === value);
          setRequest((current) => current ? { ...current, assigneeUserId: String(value) || undefined, assigneeEmploymentId: candidate?.employmentId } : current);
          return;
        }
        setRequest((current) => current ? { ...current, [name]: value } : current);
      }}
      title={`快速更新：${item.task.title}`}
      value={request}
    >
      <dl className="grid grid-cols-2 gap-2 text-xs"><div><dt className="text-gray-400">项目</dt><dd>{item.projectName}</dd></div><div><dt className="text-gray-400">截止日期</dt><dd>{item.task.dueDate ? new Date(item.task.dueDate).toLocaleDateString() : '未设置'}</dd></div><div className="col-span-2"><dt className="text-gray-400">任务说明</dt><dd>{item.task.description || '暂无任务说明'}</dd></div></dl>
    </ModalForm>
  );
}
