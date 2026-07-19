import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import {
  archiveProjectManagementProject,
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
import { useAuthStore } from '../../core/state';
import { registerWorkspaceTransitionBlocker } from '../../core/state/workspaceTransitionGuard';
import '../../features/project-management/projectManagement.css';
import { ProjectManagementPageStateView } from '../../features/project-management/components/ProjectManagementPageState';
import { ProjectManagementReversibleCommandControls } from '../../features/project-management/components/ProjectManagementReversibleCommandControls';
import {
  projectCenterPreferenceKey,
  readProjectCenterPreferences,
  rememberRecentProject,
  toggleProjectFavorite,
  writeProjectCenterPreferences,
  type ProjectCenterCollection,
  type ProjectCenterPreferences
} from '../../features/project-management/state/projectCenterPreferences';
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
  const userId = useAuthStore((state) => state.user?.userId ?? '');
  const confirm = useConfirm();
  const message = useMessage();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [keyword, setKeyword] = useState('');
  const [submittedKeyword, setSubmittedKeyword] = useState('');
  const [status, setStatus] = useState('');
  const [ownerUserId, setOwnerUserId] = useState('');
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [form, setForm] = useState<ProjectManagementProjectUpsertRequest>(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editorOpen, setEditorOpen] = useState(false);
  const [formDirty, setFormDirty] = useState(false);
  const [collection, setCollection] = useState<ProjectCenterCollection>('all');
  const [viewMode, setViewMode] = useState<'list' | 'card'>('list');
  const preferenceKey = projectCenterPreferenceKey(userId, scope.tenantId, scope.appCode);
  const [preferences, setPreferences] = useState<ProjectCenterPreferences>(() => readProjectCenterPreferences(preferenceKey));
  useEffect(() => {
    setPreferences(readProjectCenterPreferences(preferenceKey));
  }, [preferenceKey]);
  const updatePreferences = useCallback((next: ProjectCenterPreferences) => {
    setPreferences(next);
    writeProjectCenterPreferences(preferenceKey, next);
  }, [preferenceKey]);
  const projectQuery = useMemo(() => ({
    pageIndex: collection === 'all' ? pageIndex : 1,
    pageSize: collection === 'all' ? pageSize : 200,
    keyword: submittedKeyword || undefined,
    status: status || undefined,
    ownerUserId: ownerUserId.trim() || undefined
  }), [collection, ownerUserId, pageIndex, pageSize, status, submittedKeyword]);
  const projectsQuery = useQuery({
    enabled: scope.isAvailable,
    queryFn: ({ signal }) => getProjectManagementProjects(projectQuery, signal),
    queryKey: queryKeys.projectManagement.projects(scope, projectQuery)
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

  useEffect(() => registerWorkspaceTransitionBlocker('project-management-project-editor', {
    isDirty: () => formDirty,
    reason: '项目表单有未保存更改'
  }), [formDirty]);

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.projects(scope, projectQuery) });
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

  const archiveMutation = useApiMutation({
    mutationFn: (project: ProjectManagementProject) => archiveProjectManagementProject(project.id, project.versionNo),
    onError: (error) => message.error(getErrorMessage(error, '项目归档失败')),
    onSuccess: async () => {
      message.success('项目已归档并设为只读');
      await refresh();
    }
  });

  const openProject = (projectId: string) => {
    updatePreferences(rememberRecentProject(preferences, projectId));
    navigate(toProjectManagementPlatformRoute(`projects/${encodeURIComponent(projectId)}/overview`));
  };
  const toggleFavorite = useCallback(
    (projectId: string) => updatePreferences(toggleProjectFavorite(preferences, projectId)),
    [preferences, updatePreferences]
  );

  const columns: DataTableColumn<ProjectManagementProject>[] = useMemo(() => [
    { key: 'favorite', title: '收藏', width: '72px', render: (row) => <button aria-label={(preferences.favoriteProjectIds.includes(row.id) ? '取消收藏' : '收藏') + '项目 ' + row.projectName} onClick={() => toggleFavorite(row.id)} type="button">{preferences.favoriteProjectIds.includes(row.id) ? '★' : '☆'}</button> },
    { key: 'projectCode', title: '项目编码', width: '140px', responsivePriority: 100 },
    { key: 'projectName', title: '项目名称', responsivePriority: 100, render: (row) => <strong>{row.projectName}</strong> },
    { key: 'status', title: '状态', width: '118px', render: (row) => <ProjectStatus status={row.status} /> },
    { key: 'priority', title: '优先级', width: '106px', render: (row) => <PriorityBadge priority={row.priority} /> },
    { key: 'progressPercent', title: '进度', width: '142px', render: (row) => <Progress value={row.progressPercent} /> },
    { key: 'ownerDisplayName', title: '负责人', width: '140px', render: (row) => row.ownerDisplayName || '未分配' }
  ], [preferences.favoriteProjectIds, toggleFavorite]);

  if (!scope.isAvailable) {
    return <PageError description="项目管理仅支持 SYSTEM 平台工作区。请切换至 SYSTEM 后再访问项目管理。" />;
  }

  if (projectsQuery.isLoading) return <PageLoading />;
  if (projectsQuery.isError) {
    if (isHttpError(projectsQuery.error) && projectsQuery.error.status === 403) return <Page403 />;
    return <PageError action={<button type="button" onClick={() => void projectsQuery.refetch()}>重试</button>} description="项目列表加载失败，请检查当前工作区后重试。" />;
  }

  const loadedRows = projectsQuery.data?.data?.items ?? [];
  const rows = collection === 'favorites'
    ? loadedRows.filter((project) => preferences.favoriteProjectIds.includes(project.id))
    : collection === 'recent'
      ? [...loadedRows].sort((left, right) => preferences.recentProjectIds.indexOf(left.id) - preferences.recentProjectIds.indexOf(right.id))
      : loadedRows;
  const editorPermission = editingId ? 'project-management:project:edit' : 'project-management:project:add';

  return (
    <ResponsivePage
      actions={<PermissionButton code="project-management:project:add" onClick={() => { setEditingId(null); setForm(emptyForm); setFormDirty(false); setEditorOpen(true); }}>新建项目</PermissionButton>}
      className="pm-page"
      description="按项目、里程碑和任务层级组织团队执行计划。"
      eyebrow="ProjectManagement"
      title="项目管理"
      toolbar={
        <div className="flex min-w-0 flex-col gap-2">
          <div className="responsive-toolbar">
            <form className="responsive-toolbar__actions justify-start" onSubmit={(event) => { event.preventDefault(); setSubmittedKeyword(keyword.trim()); setPageIndex(1); }}>
            <input aria-label="搜索项目" value={keyword} onChange={(event) => setKeyword(event.target.value)} placeholder="搜索编码或名称" />
            <select aria-label="按项目状态筛选" value={status} onChange={(event) => { setStatus(event.target.value); setPageIndex(1); }}>
              <option value="">全部状态</option>
              {['Planning', 'Active', 'Paused', 'Completed', 'Canceled', 'Archived'].map((value) => <option key={value} value={value}>{projectStatusLabel(value)}</option>)}
            </select>
            <input aria-label="按负责人筛选" value={ownerUserId} onChange={(event) => { setOwnerUserId(event.target.value); setPageIndex(1); }} placeholder="负责人名称" />
            <button className="primary-button" type="submit">搜索</button>
            {submittedKeyword || status || ownerUserId ? <button className="ghost-button" type="button" onClick={() => { setKeyword(''); setSubmittedKeyword(''); setStatus(''); setOwnerUserId(''); setPageIndex(1); }}>清空</button> : null}
            </form>
            <div className="responsive-toolbar__actions" aria-label="项目中心集合与视图">
              {(['all', 'favorites', 'recent'] as ProjectCenterCollection[]).map((value) => <button className={collection === value ? 'primary-button' : 'ghost-button'} key={value} onClick={() => { setCollection(value); setPageIndex(1); }} type="button">{value === 'all' ? '全部项目' : value === 'favorites' ? '我的收藏' : '最近访问'}</button>)}
              <button className="ghost-button" onClick={() => setViewMode(viewMode === 'list' ? 'card' : 'list')} type="button">{viewMode === 'list' ? '卡片视图' : '列表视图'}</button>
            </div>
          </div>
          <div className="responsive-toolbar">
            <span className="text-xs text-gray-500">当前工作区共 <strong>{projectsQuery.data?.data?.total ?? 0}</strong> 个项目</span>
            <div className="responsive-toolbar__actions"><ProjectManagementReversibleCommandControls /></div>
          </div>
        </div>
      }
    >
      {rows.length === 0 ? <ProjectManagementPageStateView state="empty" /> : viewMode === 'card' ? <ProjectCardGrid favoriteProjectIds={preferences.favoriteProjectIds} onOpen={openProject} onToggleFavorite={toggleFavorite} projects={rows} /> : <DataTable
        columnSettingsKey="project-management-projects"
        columns={columns}
        emptyText="暂无符合筛选条件的项目"
        loading={projectsQuery.isFetching}
        onPageChange={setPageIndex}
        onPageSizeChange={(nextPageSize) => { setPageSize(nextPageSize); setPageIndex(1); }}
        pagination={{ current: pageIndex, pageSize, total: projectsQuery.data?.data?.total ?? 0 }}
        rowActions={(row) => <div className="pm-project-actions"><button type="button" onClick={() => openProject(row.id)}>进入项目</button><PermissionButton code="project-management:project:edit" disabled={row.status === 'Archived'} onClick={() => { setEditingId(row.id); setForm({ projectCode: row.projectCode, projectName: row.projectName, description: row.description, status: row.status, priority: row.priority, ownerUserId: row.ownerUserId, startDate: row.startDate, dueDate: row.dueDate, progressPercent: row.progressPercent, versionNo: row.versionNo }); setFormDirty(false); setEditorOpen(true); }}>编辑</PermissionButton><PermissionButton code="project-management:project:archive" disabled={row.status === 'Archived' || archiveMutation.isPending} onClick={() => confirm({ title: '归档项目', content: `项目“${row.projectName}”归档后将只读，后续修改需先由治理人员处理。`, confirmText: '归档', onConfirm: () => archiveMutation.mutate(row) })}>归档</PermissionButton><PermissionButton code="project-management:project:delete" disabled={deleteMutation.isPending} onClick={() => confirm({ title: '移入项目回收站', content: `项目“${row.projectName}”将被移入回收站，关联对象的恢复规则由服务端校验。`, confirmText: '移入回收站', onConfirm: () => deleteMutation.mutate(row) })}>删除</PermissionButton></div>}
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

function ProjectCardGrid({ projects, favoriteProjectIds, onOpen, onToggleFavorite }: { projects: ProjectManagementProject[]; favoriteProjectIds: string[]; onOpen: (projectId: string) => void; onToggleFavorite: (projectId: string) => void }) {
  return <div className="pm-task-grid" aria-label="项目卡片视图">{projects.map((project) => <article className="pm-panel" key={project.id}>
    <div className="pm-panel__heading"><div><span className="pm-panel__meta">{project.projectCode}</span><h2>{project.projectName}</h2></div><button aria-label={(favoriteProjectIds.includes(project.id) ? '取消收藏' : '收藏') + '项目 ' + project.projectName} onClick={() => onToggleFavorite(project.id)} type="button">{favoriteProjectIds.includes(project.id) ? '★' : '☆'}</button></div>
    <div className="pm-task-card__meta"><ProjectStatus status={project.status} /><PriorityBadge priority={project.priority} /></div>
    <Progress value={project.progressPercent} />
    <p className="pm-muted">{project.description || '暂无项目说明'}</p>
    <div className="pm-task-card__signals"><span>负责人：{project.ownerDisplayName || '未分配'}</span><span>截止：{project.dueDate ? new Date(project.dueDate).toLocaleDateString() : '未设置'}</span></div>
    <button className="pm-task-card__open" onClick={() => onOpen(project.id)} type="button">进入项目</button>
  </article>)}</div>;
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
