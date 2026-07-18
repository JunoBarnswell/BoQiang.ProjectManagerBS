import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';

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
import { usePermission } from '../../core/auth/usePermission';
import { isHttpError } from '../../core/http/httpError';
import { queryKeys } from '../../core/query/queryKeys';
import { useApiMutation } from '../../core/query/useApiMutation';
import '../../features/project-management/projectManagement.css';
import { ProjectManagementPageStateView } from '../../features/project-management/components/ProjectManagementPageState';
import { toProjectManagementPlatformRoute } from '../../features/project-management/state/projectManagementPlatformRoutes';
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

const projectFormFields: FormFieldConfig<ProjectManagementProjectUpsertRequest>[] = [
  { label: '项目编码', name: 'projectCode', placeholder: '例如 PM-2026-001', required: true, type: 'text' },
  { label: '项目名称', name: 'projectName', placeholder: '输入项目名称', required: true, type: 'text' },
  { label: '项目状态', name: 'status', options: ['Planning', 'Active', 'Paused', 'Completed', 'Canceled', 'Archived'].map((value) => ({ label: projectStatusLabel(value), value })), type: 'select' },
  { label: '优先级', name: 'priority', options: ['Low', 'Medium', 'High', 'Urgent'].map((value) => ({ label: priorityLabel(value), value })), type: 'select' },
  { label: '开始日期', name: 'startDate', type: 'date' },
  { label: '截止日期', name: 'dueDate', type: 'date' },
  { label: '项目说明', name: 'description', placeholder: '说明目标、范围或当前约束', rows: 4, span: 2, type: 'textarea' }
];

export function ProjectManagementPage() {
  const scope = useProjectManagementWorkspaceScope();
  const { hasPermission: canViewProjectManagement } = usePermission('project-management:project:view');
  const { hasPermission: canExportSync } = usePermission('project-management:sync:export');
  const { hasPermission: canImportSync } = usePermission('project-management:sync:import');
  const { hasPermission: canViewAudit } = usePermission('project-management:audit:view');
  const confirm = useConfirm();
  const message = useMessage();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [keyword, setKeyword] = useState('');
  const [submittedKeyword, setSubmittedKeyword] = useState('');
  const [status, setStatus] = useState('');
  const [ownerUserId, setOwnerUserId] = useState('');
  const [form, setForm] = useState<ProjectManagementProjectUpsertRequest>(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editorOpen, setEditorOpen] = useState(false);
  const [formDirty, setFormDirty] = useState(false);
  const projectsQuery = useQuery({
    enabled: scope.isAvailable,
    queryFn: ({ signal }) => getProjectManagementProjects({ pageIndex: 1, pageSize: 100, keyword: submittedKeyword || undefined, status: status || undefined, ownerUserId: ownerUserId.trim() || undefined }, signal),
    queryKey: queryKeys.projectManagement.projects(scope, { pageIndex: 1, pageSize: 100, keyword: submittedKeyword || undefined, status: status || undefined, ownerUserId: ownerUserId.trim() || undefined })
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
    await queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.projects(scope, { pageIndex: 1, pageSize: 100, keyword: submittedKeyword || undefined, status: status || undefined, ownerUserId: ownerUserId.trim() || undefined }) });
  };

  const closeEditor = () => {
    setEditorOpen(false);
    setEditingId(null);
    setForm(emptyForm);
    setFormDirty(false);
  };

  const saveMutation = useApiMutation({
    mutationFn: () => editingId ? updateProjectManagementProject(editingId, form) : createProjectManagementProject(form),
    onError: (error) => message.error(getErrorMessage(error, editingId ? '项目保存失败' : '项目创建失败')),
    onSuccess: async () => {
      message.success(editingId ? '项目已更新' : '项目已创建');
      closeEditor();
      await refresh();
    }
  });

  const deleteMutation = useApiMutation({
    mutationFn: (project: ProjectManagementProject) => deleteProjectManagementProject(project.id, project.versionNo),
    onError: (error) => message.error(getErrorMessage(error, '项目删除失败')),
    onSuccess: async () => {
      message.success('项目已移入回收站');
      await refresh();
    }
  });

  const columns: DataTableColumn<ProjectManagementProject>[] = useMemo(() => [
    { key: 'projectCode', title: '项目编码', width: '140px', responsivePriority: 100 },
    { key: 'projectName', title: '项目名称', responsivePriority: 100, render: (row) => <strong>{row.projectName}</strong> },
    { key: 'status', title: '状态', width: '118px', render: (row) => <ProjectStatus status={row.status} /> },
    { key: 'priority', title: '优先级', width: '106px', render: (row) => <PriorityBadge priority={row.priority} /> },
    { key: 'progressPercent', title: '进度', width: '142px', render: (row) => <Progress value={row.progressPercent} /> },
    { key: 'ownerUserId', title: '负责人', width: '140px', render: (row) => row.ownerUserId || '未分配' }
  ], []);

  if (!scope.isAvailable) {
    return <PageError description="项目管理仅支持 SYSTEM 平台工作区。请切换至 SYSTEM 后再访问项目管理。" />;
  }

  if (projectsQuery.isLoading) return <PageLoading />;
  if (projectsQuery.isError) {
    if (isHttpError(projectsQuery.error) && projectsQuery.error.status === 403) return <Page403 />;
    return <PageError action={<button type="button" onClick={() => void projectsQuery.refetch()}>重试</button>} description="项目列表加载失败，请检查当前工作区后重试。" />;
  }

  const rows = projectsQuery.data?.data?.items ?? [];
  const editorPermission = editingId ? 'project-management:project:edit' : 'project-management:project:add';

  return (
    <ResponsivePage
      actions={<PermissionButton code="project-management:project:add" onClick={() => { setEditingId(null); setForm(emptyForm); setFormDirty(false); setEditorOpen(true); }}>新建项目</PermissionButton>}
      className="pm-page"
      description="按项目、里程碑和任务层级组织团队执行计划。"
      eyebrow="ProjectManagement"
      title="项目管理"
      toolbar={
        <div className="pm-toolbar-summary">
          <form className="flex flex-wrap items-center gap-2" onSubmit={(event) => { event.preventDefault(); setSubmittedKeyword(keyword.trim()); }}>
            <input aria-label="搜索项目" value={keyword} onChange={(event) => setKeyword(event.target.value)} placeholder="搜索编码或名称" />
            <select aria-label="按项目状态筛选" value={status} onChange={(event) => setStatus(event.target.value)}>
              <option value="">全部状态</option>
              {['Planning', 'Active', 'Paused', 'Completed', 'Canceled', 'Archived'].map((value) => <option key={value} value={value}>{projectStatusLabel(value)}</option>)}
            </select>
            <input aria-label="按负责人筛选" value={ownerUserId} onChange={(event) => setOwnerUserId(event.target.value)} placeholder="负责人账号" />
            <button type="submit">搜索</button>
            {submittedKeyword || status || ownerUserId ? <button type="button" onClick={() => { setKeyword(''); setSubmittedKeyword(''); setStatus(''); setOwnerUserId(''); }}>清空</button> : null}
          </form>
          <span>当前工作区共 <strong>{projectsQuery.data?.data?.total ?? 0}</strong> 个项目</span>
        </div>
      }
    >
      {canViewProjectManagement || canExportSync || canImportSync || canViewAudit ? (
        <section aria-labelledby="project-management-workbench-title" className="mb-4 rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <h2 id="project-management-workbench-title" className="text-base font-semibold text-gray-900">项目管理工作台</h2>
              <p className="mt-1 text-sm text-gray-600">从当前账号可访问的项目管理能力快速进入对应页面。</p>
            </div>
            <span className="rounded-full bg-gray-100 px-2 py-1 text-xs text-gray-600">当前工作区</span>
          </div>
          <nav aria-label="项目管理工作台快捷入口" className="mt-3 flex flex-wrap gap-2">
            {canViewProjectManagement ? <>
              <Link className="rounded border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50" to={toProjectManagementPlatformRoute('project-search')}>项目搜索</Link>
              <Link className="rounded border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50" to={toProjectManagementPlatformRoute('my-work')}>我的工作</Link>
              <Link className="rounded border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50" to={toProjectManagementPlatformRoute('project-recycle-bin')}>回收站</Link>
              <Link className="rounded border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50" to={toProjectManagementPlatformRoute('project-data-space')}>数据空间</Link>
            </> : null}
            {canExportSync || canImportSync ? <Link className="rounded border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50" to={toProjectManagementPlatformRoute('project-sync')}>同步</Link> : null}
            {canViewAudit ? <Link className="rounded border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50" to={toProjectManagementPlatformRoute('project-audit-center')}>审计中心</Link> : null}
          </nav>
          <p className="mt-3 text-xs text-gray-500">说明：SYSTEM 工作台暂不提供物理数据库备份或恢复入口。</p>
        </section>
      ) : null}
      {rows.length === 0 ? <ProjectManagementPageStateView state="empty" /> : <DataTable
        columnSettingsKey="project-management-projects"
        columns={columns}
        emptyText="暂无符合筛选条件的项目"
        loading={projectsQuery.isFetching}
        rowActions={(row) => <div className="pm-project-actions"><button type="button" onClick={() => navigate(toProjectManagementPlatformRoute(`projects/${encodeURIComponent(row.id)}/overview`))}>进入项目</button><PermissionButton code="project-management:project:edit" onClick={() => { setEditingId(row.id); setForm({ projectCode: row.projectCode, projectName: row.projectName, description: row.description, status: row.status, priority: row.priority, ownerUserId: row.ownerUserId, startDate: row.startDate, dueDate: row.dueDate, progressPercent: row.progressPercent, versionNo: row.versionNo }); setFormDirty(false); setEditorOpen(true); }}>编辑</PermissionButton><PermissionButton code="project-management:project:delete" disabled={deleteMutation.isPending} onClick={() => confirm({ title: '移入项目回收站', content: `项目“${row.projectName}”将被移入回收站，关联对象的恢复规则由服务端校验。`, confirmText: '移入回收站', onConfirm: () => deleteMutation.mutate(row) })}>删除</PermissionButton></div>}
        rowKey={(row) => row.id}
        rows={rows}
        showColumnSettings
      />}
      <PermissionGuard code={editorPermission} fallback={null}>
        <ModalForm
          actions={[
            { label: '取消', onClick: closeEditor, variant: 'ghost' },
            { label: editingId ? '保存修改' : '创建项目', loading: saveMutation.isPending, onClick: () => saveMutation.mutate(), variant: 'primary' }
          ]}
          fields={projectFormFields}
          open={editorOpen}
          onClose={closeEditor}
          onValueChange={(name, value) => { setForm((current) => ({ ...current, [name]: value })); setFormDirty(true); }}
          title={editingId ? '编辑项目' : '新建项目'}
          value={form}
        >
          项目状态和优先级是可见的业务语义；保存时由服务端执行版本和权限校验。
        </ModalForm>
      </PermissionGuard>
    </ResponsivePage>
  );
}

function ProjectStatus({ status }: { status: string }) {
  const tone = status === 'Active' ? 'in-progress' : status === 'Completed' ? 'done' : status === 'Paused' ? 'blocked' : status === 'Canceled' || status === 'Archived' ? 'cancelled' : 'todo';
  return <span className={`pm-status-badge pm-status-badge--${tone}`}>{projectStatusLabel(status)}</span>;
}

function PriorityBadge({ priority }: { priority: string }) {
  return <span className={`pm-priority-badge pm-priority-badge--${priority.toLowerCase()}`}>{priorityLabel(priority)}</span>;
}

function Progress({ value }: { value: number }) {
  return <div className="pm-progress"><progress aria-label={`项目进度 ${value}%`} max={100} value={value} /><span>{value}%</span></div>;
}

function projectStatusLabel(status: string) {
  return ({ Planning: '规划中', Active: '进行中', Paused: '已暂停', Completed: '已完成', Canceled: '已取消', Archived: '已归档' } as Record<string, string>)[status] ?? status;
}

function priorityLabel(priority: string) {
  return ({ Low: '低', Medium: '中', High: '高', Urgent: '紧急' } as Record<string, string>)[priority] ?? priority;
}
