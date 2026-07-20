import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';

import {
  createProjectManagementMilestone,
  deleteProjectManagementMilestone,
  getProjectManagementMemberCandidates,
  getProjectManagementMilestones,
  updateProjectManagementMilestone,
} from '../../api/project-management/projectManagement.api';
import type {
  ProjectManagementMilestone,
  ProjectManagementMilestoneUpsertRequest,
} from '../../api/project-management/projectManagement.types';
import { isHttpError } from '../../core/http/httpError';
import { projectManagementQueryKeys } from '../../core/query/projectManagementQueryKeys';
import { useApiMutation } from '../../core/query/useApiMutation';
import { milestoneStatusLabel, milestoneStatuses } from '../../features/project-management/projectManagementPresentation';
import { useProjectManagementWorkspaceScope } from '../../features/project-management/state/projectManagementWorkspaceScope';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { PermissionGuard } from '../../shared/auth/PermissionGuard';
import { useConfirm } from '../../shared/feedback/useConfirm';
import { useMessage } from '../../shared/feedback/useMessage';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import { ModalForm } from '../../shared/forms/ModalForm';
import { ResponsivePage } from '../../shared/responsive/ResponsivePage';
import { Page403 } from '../../shared/status/Page403';
import { PageError } from '../../shared/status/PageError';
import { PageLoading } from '../../shared/status/PageLoading';
import { getErrorMessage } from '../../shared/utils/errorMessage';

const emptyForm: ProjectManagementMilestoneUpsertRequest = {
  milestoneName: '',
  status: 'Planned',
  progressPercent: 0,
  sortOrder: 0,
  versionNo: 0,
};

export function ProjectManagementMilestonesPage() {
  const scope = useProjectManagementWorkspaceScope();
  const { projectId = '' } = useParams<{ projectId: string }>();
  const confirm = useConfirm();
  const message = useMessage();
  const queryClient = useQueryClient();
  const [form, setForm] = useState<ProjectManagementMilestoneUpsertRequest>(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editorOpen, setEditorOpen] = useState(false);
  const [dirty, setDirty] = useState(false);
  const milestonesQuery = useQuery({
    queryKey: projectManagementQueryKeys.milestones(scope, projectId),
    queryFn: ({ signal }) => getProjectManagementMilestones(projectId, signal),
    enabled: scope.isAvailable && Boolean(projectId),
  });
  const candidatesQuery = useQuery({
    queryKey: projectManagementQueryKeys.memberCandidates(scope, { pageIndex: 1, pageSize: 100, projectId }),
    queryFn: ({ signal }) => getProjectManagementMemberCandidates({ pageIndex: 1, pageSize: 100, projectId }, signal),
    enabled: scope.isAvailable && Boolean(projectId) && editorOpen,
  });

  useEffect(() => {
    const handler = (event: BeforeUnloadEvent) => {
      if (!dirty) return;
      event.preventDefault();
      event.returnValue = '里程碑表单有未保存更改。';
    };
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [dirty]);

  const refresh = () => queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.milestones(scope, projectId) });
  const closeEditor = () => {
    setEditorOpen(false);
    setEditingId(null);
    setForm(emptyForm);
    setDirty(false);
  };
  const saveMutation = useApiMutation({
    mutationFn: () => editingId
      ? updateProjectManagementMilestone(projectId, editingId, form)
      : createProjectManagementMilestone(projectId, form),
    onError: (error) => message.error(getErrorMessage(error, editingId ? '里程碑保存失败' : '里程碑创建失败')),
    onSuccess: async () => {
      message.success(editingId ? '里程碑已更新' : '里程碑已创建');
      closeEditor();
      await refresh();
    },
  });
  const deleteMutation = useApiMutation({
    mutationFn: (milestone: ProjectManagementMilestone) => deleteProjectManagementMilestone(projectId, milestone.id, milestone.versionNo),
    onError: (error) => message.error(getErrorMessage(error, '里程碑删除失败')),
    onSuccess: async () => {
      message.success('里程碑已删除');
      await refresh();
    },
  });

  const milestones = milestonesQuery.data?.data.items ?? [];
  const editingMilestone = milestones.find((item) => item.id === editingId);
  const progressIsDerived = Boolean(editingMilestone && editingMilestone.leafTaskCount > 0);
  const candidates = useMemo(() => candidatesQuery.data?.data.items ?? [], [candidatesQuery.data?.data.items]);
  const ownerOptions = useMemo(() => {
    const selectedOwner = form.ownerUserId && !candidates.some((candidate) => candidate.userId === form.ownerUserId)
      ? [{ label: '当前负责人暂不可用', value: form.ownerUserId }]
      : [];
    return [
      ...selectedOwner,
      ...candidates
        .filter((candidate) => candidate.isSelectable)
        .map((candidate) => ({ label: `${candidate.displayName || candidate.userName} · ${candidate.employmentName}`, value: candidate.userId })),
    ];
  }, [candidates, form.ownerUserId]);
  const fields: FormFieldConfig<ProjectManagementMilestoneUpsertRequest>[] = [
    { label: '基本信息', name: 'milestoneName', placeholder: '例如：M1 需求验收', required: true, section: '基本信息', type: 'text' },
    { label: '状态', name: 'status', options: milestoneStatuses.map((status) => ({ label: milestoneStatusLabel(status), value: status })), required: true, section: '基本信息', type: 'select' },
    { label: '负责人', name: 'ownerUserId', options: ownerOptions, emptyOptionLabel: candidatesQuery.isLoading ? '正在加载候选人…' : '选择项目负责人或成员', helpText: '仅可选择当前项目负责人或有效项目成员，保存时由服务端再次校验。', section: '计划与负责人', span: 2, type: 'select' },
    { label: '开始日期', name: 'startDate', section: '计划与负责人', type: 'date' },
    { label: '目标日期', name: 'dueDate', section: '计划与负责人', type: 'date' },
    { label: '完成进度', name: 'progressPercent', disabled: progressIsDerived, helpText: progressIsDerived ? `已关联 ${editingMilestone?.leafTaskCount ?? 0} 个叶子任务，进度由任务完成度自动计算。` : '暂无叶子任务时可手动维护进度。', max: 100, min: 0, section: '进度与说明', span: 2, step: 1, type: 'range' },
    { label: '排序', name: 'sortOrder', min: 0, section: '进度与说明', type: 'number' },
    { label: '里程碑说明', name: 'description', placeholder: '说明交付目标、范围或风险', rows: 4, section: '进度与说明', span: 2, type: 'textarea' },
  ];

  if (milestonesQuery.isLoading) return <PageLoading />;
  if (milestonesQuery.isError) {
    if (isHttpError(milestonesQuery.error) && milestonesQuery.error.status === 403) return <Page403 />;
    return <PageError description="里程碑加载失败" action={<button type="button" onClick={() => void milestonesQuery.refetch()}>重试</button>} />;
  }
  const openEditor = (milestone?: ProjectManagementMilestone) => {
    setEditingId(milestone?.id ?? null);
    setForm(milestone ? {
      milestoneName: milestone.milestoneName,
      description: milestone.description,
      ownerUserId: milestone.ownerUserId,
      status: milestone.status,
      startDate: milestone.startDate,
      dueDate: milestone.dueDate,
      progressPercent: milestone.progressPercent,
      sortOrder: milestone.sortOrder,
      versionNo: milestone.versionNo,
    } : emptyForm);
    setDirty(false);
    setEditorOpen(true);
  };

  return (
    <ResponsivePage
      title="里程碑管理"
      description="定义项目阶段目标、负责人和交付健康度。"
      eyebrow="ProjectManagement / Milestones"
      actions={<PermissionButton code="project-management:milestone:manage" onClick={() => openEditor()}>新建里程碑</PermissionButton>}
    >
      {milestones.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 p-8 text-center text-sm text-gray-500">暂无里程碑</div>
      ) : (
        <div className="space-y-2">
          {milestones.map((milestone) => (
            <article className="rounded-lg border border-gray-200 p-4" key={milestone.id}>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <h3 className="font-semibold">{milestone.milestoneName}</h3>
                  <p className="mt-1 text-sm text-gray-500">{milestoneStatusLabel(milestone.status)} · 健康度 {milestone.healthStatus} · 负责人 {milestone.ownerDisplayName || candidates.find((candidate) => candidate.userId === milestone.ownerUserId)?.displayName || (milestone.ownerUserId ? '用户别名暂不可用' : '未设置')}</p>
                  <p className="mt-1 text-sm text-gray-500">目标日期 {milestone.dueDate?.slice(0, 10) ?? '未设置'} · 叶子任务 {milestone.completedLeafTaskCount}/{milestone.leafTaskCount}</p>
                </div>
                <div className="flex gap-2">
                  <PermissionButton code="project-management:milestone:manage" onClick={() => openEditor(milestone)}>编辑</PermissionButton>
                  <PermissionButton
                    code="project-management:milestone:manage"
                    disabled={deleteMutation.isPending}
                    onClick={() => confirm({
                      title: '删除里程碑',
                      content: `确定删除“${milestone.milestoneName}”吗？该操作会移除该里程碑，叶子任务不会被删除。`,
                      confirmText: '删除里程碑',
                      onConfirm: () => deleteMutation.mutate(milestone),
                    })}
                  >删除</PermissionButton>
                </div>
              </div>
              <div className="mt-3 h-2 overflow-hidden rounded bg-gray-100"><div className="h-full bg-blue-600" style={{ width: `${Math.min(100, Math.max(0, milestone.progressPercent))}%` }} /></div>
              <div className="mt-1 text-right text-xs text-gray-500">{milestone.progressPercent}%{milestone.leafTaskCount > 0 ? '（按叶子任务计算）' : ''}</div>
            </article>
          ))}
        </div>
      )}
      <PermissionGuard code="project-management:milestone:manage" fallback={null}>
        <ModalForm
          actions={[
            { label: '取消', onClick: closeEditor, variant: 'ghost' },
            { label: editingId ? '保存修改' : '创建里程碑', disabled: !form.milestoneName.trim(), loading: saveMutation.isPending, onClick: () => saveMutation.mutate(), variant: 'primary' },
          ]}
          fields={fields}
          open={editorOpen}
          onClose={closeEditor}
          onValueChange={(name, value) => { setForm((current) => ({ ...current, [name]: value })); setDirty(true); }}
          title={editingId ? '编辑里程碑' : '新建里程碑'}
          value={form}
        >
          {candidatesQuery.isError ? '负责人候选人暂时无法加载；请重试后再保存，服务端仍会校验负责人资格。' : '状态、负责人和目标日期共同决定里程碑的交付健康度。'}
        </ModalForm>
      </PermissionGuard>
    </ResponsivePage>
  );
}
