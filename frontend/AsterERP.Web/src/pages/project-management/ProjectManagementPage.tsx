import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import {
  createProjectManagementProject,
  deleteProjectManagementProject,
  getProjectManagementProjects,
  updateProjectManagementProject
} from '../../api/project-management/projectManagement.api';
import type {
  ProjectManagementProject,
  ProjectManagementProjectUpsertRequest
} from '../../api/project-management/projectManagement.types';
import { isHttpError } from '../../core/http/httpError';
import { queryKeys } from '../../core/query/queryKeys';
import { useApiMutation } from '../../core/query/useApiMutation';
import { ProjectManagementPageStateView } from '../../features/project-management/components/ProjectManagementPageState';
import { useProjectManagementWorkspaceScope } from '../../features/project-management/state/projectManagementWorkspaceScope';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { PermissionGuard } from '../../shared/auth/PermissionGuard';
import { useMessage } from '../../shared/feedback/useMessage';
import { ResponsivePage } from '../../shared/responsive/ResponsivePage';
import { Page403 } from '../../shared/status/Page403';
import { PageError } from '../../shared/status/PageError';
import { PageLoading } from '../../shared/status/PageLoading';
import { DataTable } from '../../shared/table/DataTable';
import type { DataTableColumn } from '../../shared/table/tableTypes';
import { getErrorMessage } from '../../shared/utils/errorMessage';

const emptyForm: ProjectManagementProjectUpsertRequest = {
  projectCode: '',
  projectName: '',
  status: 'Planning',
  priority: 'Medium',
  progressPercent: 0,
  versionNo: 0
};

export function ProjectManagementPage() {
  const scope = useProjectManagementWorkspaceScope();
  const message = useMessage();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [keyword, setKeyword] = useState('');
  const [submittedKeyword, setSubmittedKeyword] = useState('');
  const [form, setForm] = useState<ProjectManagementProjectUpsertRequest>(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [formDirty, setFormDirty] = useState(false);
  const projectsQuery = useQuery({
    enabled: scope.isAvailable,
    queryFn: ({ signal }) => getProjectManagementProjects({ pageIndex: 1, pageSize: 100, keyword: submittedKeyword || undefined }, signal),
    queryKey: queryKeys.projectManagement.projects(scope, { pageIndex: 1, pageSize: 100, keyword: submittedKeyword || undefined })
  });

  useEffect(() => {
    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      if (!formDirty) return;
      event.preventDefault();
      event.returnValue = '当前项目表单有未保存更改。';
    };
    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [formDirty]);

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.projects(scope, { pageIndex: 1, pageSize: 100, keyword: submittedKeyword || undefined }) });
  };

  const saveMutation = useApiMutation({
    mutationFn: () => editingId ? updateProjectManagementProject(editingId, form) : createProjectManagementProject(form),
    onError: (error) => message.error(getErrorMessage(error, editingId ? '项目保存失败' : '项目创建失败')),
    onSuccess: async () => {
      message.success(editingId ? '项目已更新' : '项目已创建');
      setForm(emptyForm);
      setEditingId(null);
      setFormDirty(false);
      await refresh();
    }
  });

  const deleteMutation = useApiMutation({
    mutationFn: (project: ProjectManagementProject) => deleteProjectManagementProject(project.id, project.versionNo),
    onError: (error) => message.error(getErrorMessage(error, '项目删除失败')),
    onSuccess: async () => {
      message.success('项目已删除');
      await refresh();
    }
  });

  const columns: DataTableColumn<ProjectManagementProject>[] = useMemo(() => [
    { key: 'projectCode', title: '项目编码', width: '140px', responsivePriority: 100 },
    { key: 'projectName', title: '项目名称', responsivePriority: 100 },
    { key: 'status', title: '状态', width: '110px' },
    { key: 'priority', title: '优先级', width: '100px' },
    { key: 'progressPercent', title: '进度', width: '90px', render: (row) => `${row.progressPercent}%` },
    { key: 'ownerUserId', title: '负责人', width: '140px' }
  ], []);

  if (projectsQuery.isLoading) return <PageLoading />;
  if (projectsQuery.isError) {
    if (isHttpError(projectsQuery.error) && projectsQuery.error.status === 403) return <Page403 />;
    return <PageError action={<button type="button" onClick={() => void projectsQuery.refetch()}>重试</button>} description="项目列表加载失败" />;
  }

  const rows = projectsQuery.data?.data?.items ?? [];

  return (
    <ResponsivePage
      title="项目管理"
      description="按项目、里程碑和任务层级组织团队执行计划。"
      eyebrow="ProjectManagement"
      toolbar={
        <div className="flex flex-wrap items-center gap-2">
          <form className="flex items-center gap-2" onSubmit={(event) => { event.preventDefault(); setSubmittedKeyword(keyword.trim()); }}>
            <input aria-label="搜索项目" value={keyword} onChange={(event) => setKeyword(event.target.value)} placeholder="搜索编码或名称" />
            <button type="submit">搜索</button>
            {submittedKeyword ? <button type="button" onClick={() => { setKeyword(''); setSubmittedKeyword(''); }}>清空</button> : null}
          </form>
          <span className="text-sm text-gray-500">共 {projectsQuery.data?.data?.total ?? 0} 个项目</span>
        </div>
      }
    >
      <PermissionGuard code={editingId ? 'project-management:project:edit' : 'project-management:project:add'} fallback={null}>
      <div className="mb-4 rounded-lg border border-gray-200 p-4">
        <div className="mb-3 text-sm font-semibold">{editingId ? '编辑项目' : '新建项目'}</div>
        <div className="grid gap-2 md:grid-cols-4">
          <input aria-label="项目编码" value={form.projectCode} onChange={(event) => { setForm({ ...form, projectCode: event.target.value }); setFormDirty(true); }} placeholder="项目编码" />
          <input aria-label="项目名称" value={form.projectName} onChange={(event) => { setForm({ ...form, projectName: event.target.value }); setFormDirty(true); }} placeholder="项目名称" />
          <select aria-label="项目状态" value={form.status} onChange={(event) => { setForm({ ...form, status: event.target.value }); setFormDirty(true); }}>
            {['Planning', 'Active', 'Paused', 'Completed', 'Canceled', 'Archived'].map((status) => <option key={status}>{status}</option>)}
          </select>
          <select aria-label="优先级" value={form.priority} onChange={(event) => { setForm({ ...form, priority: event.target.value }); setFormDirty(true); }}>
            {['Low', 'Medium', 'High', 'Urgent'].map((priority) => <option key={priority}>{priority}</option>)}
          </select>
        </div>
        <div className="mt-3 flex gap-2">
          <PermissionButton type="submit" code={editingId ? 'project-management:project:edit' : 'project-management:project:add'} disabled={saveMutation.isPending} onClick={() => saveMutation.mutate()}>{saveMutation.isPending ? '保存中…' : editingId ? '保存修改' : '创建项目'}</PermissionButton>
          {editingId ? <button type="button" onClick={() => { setEditingId(null); setForm(emptyForm); setFormDirty(false); }}>取消编辑</button> : null}
        </div>
      </div>
      </PermissionGuard>
      {rows.length === 0 ? <ProjectManagementPageStateView state="empty" /> : <DataTable
        columnSettingsKey="project-management-projects"
        columns={columns}
        emptyText="暂无项目"
        loading={projectsQuery.isFetching}
         rowActions={(row) => <div className="flex gap-2"><button type="button" onClick={() => navigate(`/projects/${encodeURIComponent(row.id)}/overview`)}>进入项目</button><PermissionButton code="project-management:project:edit" onClick={() => { setEditingId(row.id); setForm({ projectCode: row.projectCode, projectName: row.projectName, description: row.description, status: row.status, priority: row.priority, ownerUserId: row.ownerUserId, progressPercent: row.progressPercent, versionNo: row.versionNo }); setFormDirty(false); }}>编辑</PermissionButton><PermissionButton code="project-management:project:delete" disabled={deleteMutation.isPending} onClick={() => deleteMutation.mutate(row)}>删除</PermissionButton></div>}
        rowKey={(row) => row.id}
        rows={rows}
        showColumnSettings
      />}
    </ResponsivePage>
  );
}
