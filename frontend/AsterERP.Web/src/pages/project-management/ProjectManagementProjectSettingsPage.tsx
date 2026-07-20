import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';

import { getProjectManagementMemberCandidates, updateProjectManagementProject } from '../../api/project-management/projectManagement.api';
import type { ProjectManagementProjectUpsertRequest } from '../../api/project-management/projectManagement.types';
import { usePermission } from '../../core/auth/usePermission';
import { isHttpError } from '../../core/http/httpError';
import { projectManagementQueryKeys } from '../../core/query/projectManagementQueryKeys';
import { useApiMutation } from '../../core/query/useApiMutation';
import { getProjectManagementDashboardOverview } from '../../features/project-management/dashboard/projectManagementDashboard.api';
import { useProjectManagementWorkspaceScope } from '../../features/project-management/state/projectManagementWorkspaceScope';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { useMessage } from '../../shared/feedback/useMessage';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import { ModalForm } from '../../shared/forms/ModalForm';
import { ResponsivePage } from '../../shared/responsive/ResponsivePage';
import { Page403 } from '../../shared/status/Page403';
import { PageError } from '../../shared/status/PageError';
import { PageLoading } from '../../shared/status/PageLoading';
import { getErrorMessage } from '../../shared/utils/errorMessage';

export function ProjectManagementProjectSettingsPage() {
  const { projectId = '' } = useParams<{ projectId: string }>();
  const scope = useProjectManagementWorkspaceScope();
  const queryClient = useQueryClient();
  const message = useMessage();
  const { hasPermission: canEdit } = usePermission('project-management:project:edit');
  const [editorOpen, setEditorOpen] = useState(false);
  const [form, setForm] = useState<ProjectManagementProjectUpsertRequest>({ projectCode: '', projectName: '' });
  const overviewQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId),
    queryFn: ({ signal }) => getProjectManagementDashboardOverview({ pageIndex: 1, pageSize: 1, projectId }, signal),
    queryKey: projectManagementQueryKeys.overview(scope, { pageIndex: 1, pageSize: 1, projectId }),
  });
  const candidatesQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId),
    queryKey: projectManagementQueryKeys.memberCandidates(scope, { pageIndex: 1, pageSize: 100, projectId }),
    queryFn: ({ signal }) => getProjectManagementMemberCandidates({ pageIndex: 1, pageSize: 100, projectId }, signal),
  });
  const project = overviewQuery.data?.data?.items[0]?.project;
  const fields = useMemo<FormFieldConfig<ProjectManagementProjectUpsertRequest>[]>(() => {
    const candidates = candidatesQuery.data?.data.items ?? [];
    const ownerOptions = candidates
      .filter((candidate) => candidate.isSelectable)
      .map((candidate) => ({ label: candidate.displayName || candidate.userName, value: candidate.userId }));
    if (project?.ownerUserId && !ownerOptions.some((option) => option.value === project.ownerUserId)) {
      ownerOptions.unshift({ label: project.ownerDisplayName || '当前负责人暂不可用', value: project.ownerUserId });
    }
    return [
      { label: '项目编码', name: 'projectCode', required: true, type: 'text' },
      { label: '项目名称', name: 'projectName', required: true, type: 'text' },
      { label: '状态', name: 'status', options: ['Planning', 'Active', 'Paused', 'Completed', 'Canceled', 'Archived'].map((value) => ({ label: value, value })), type: 'select' },
      { label: '优先级', name: 'priority', options: ['Low', 'Medium', 'High', 'Urgent'].map((value) => ({ label: value, value })), type: 'select' },
      { emptyOptionLabel: '未分配', label: '负责人', name: 'ownerUserId', options: ownerOptions, type: 'select' },
      { label: 'WIP 限制', min: 1, name: 'wipLimit', type: 'number' },
      { label: '开始日期', name: 'startDate', type: 'date' },
      { label: '截止日期', name: 'dueDate', type: 'date' },
      { label: '项目说明', name: 'description', rows: 4, span: 2, type: 'textarea' },
    ];
  }, [candidatesQuery.data?.data.items, project?.ownerDisplayName, project?.ownerUserId]);

  useEffect(() => {
    if (!project) return;
    setForm({
      description: project.description,
      dueDate: project.dueDate,
      ownerUserId: project.ownerUserId,
      priority: project.priority,
      progressPercent: project.progressPercent,
      projectCode: project.projectCode,
      projectName: project.projectName,
      startDate: project.startDate,
      status: project.status,
      versionNo: project.versionNo,
      wipLimit: project.wipLimit,
    });
  }, [project]);

  const saveMutation = useApiMutation({
    mutationFn: () => updateProjectManagementProject(projectId, form),
    onError: (error) => message.error(getErrorMessage(error, '项目设置保存失败')),
    onSuccess: async () => {
      message.success('项目设置已保存');
      setEditorOpen(false);
      await queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.overview(scope, { pageIndex: 1, pageSize: 1, projectId }) });
    },
  });

  if (overviewQuery.isLoading) return <PageLoading />;
  if (overviewQuery.isError) {
    if (isHttpError(overviewQuery.error) && overviewQuery.error.status === 403) return <Page403 />;
    return <PageError action={<button onClick={() => void overviewQuery.refetch()} type="button">重试</button>} description="项目设置加载失败" />;
  }
  if (!project) return <PageError description="项目不存在或当前账号无权访问。" />;

  return <ResponsivePage className="pm-page" description="维护项目执行边界、计划日期、负责人和 WIP 限制。" eyebrow="ProjectManagement / Settings" title="项目设置" actions={<PermissionButton code="project-management:project:edit" disabled={!canEdit || project.status === 'Archived'} onClick={() => setEditorOpen(true)}>编辑项目</PermissionButton>}>
    <section className="pm-panel" aria-labelledby="project-settings-summary-title">
      <div className="pm-panel__heading"><div><h2 id="project-settings-summary-title">{project.projectName}</h2><p className="pm-panel__meta">{project.projectCode} · 当前状态 {project.status}</p></div></div>
      <dl className="pm-settings-summary">
        <div><dt>负责人</dt><dd>{project.ownerDisplayName || '未分配'}</dd></div>
        <div><dt>WIP 限制</dt><dd>{project.wipLimit ?? '未设置'}</dd></div>
        <div><dt>开始日期</dt><dd>{project.startDate || '未设置'}</dd></div>
        <div><dt>截止日期</dt><dd>{project.dueDate || '未设置'}</dd></div>
      </dl>
    </section>
    <ModalForm
      actions={[{ label: '取消', onClick: () => setEditorOpen(false), variant: 'ghost' }, { label: '保存设置', loading: saveMutation.isPending, onClick: () => saveMutation.mutate(), variant: 'primary' }]}
      fields={fields}
      onClose={() => setEditorOpen(false)}
      onValueChange={(name, value) => setForm((current) => ({ ...current, [name]: value }))}
      open={editorOpen}
      title="编辑项目设置"
      value={form}
    >
      项目设置保存仍由服务端执行版本、权限和归档状态校验。
    </ModalForm>
  </ResponsivePage>;
}
