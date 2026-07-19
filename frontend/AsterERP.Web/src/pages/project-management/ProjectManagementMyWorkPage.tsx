import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';

import { getProjectManagementMyWork, getProjectManagementMyWorkProjectOptions, updateProjectManagementTask } from '../../api/project-management/projectManagement.api';
import type { ProjectManagementMyWorkCategory, ProjectManagementTaskUpsertRequest } from '../../api/project-management/projectManagement.types';
import { isHttpError } from '../../core/http/httpError';
import { queryKeys } from '../../core/query/queryKeys';
import { useApiMutation } from '../../core/query/useApiMutation';
import { MyWorkTaskCommandPanel } from '../../features/project-management/my-work/MyWorkTaskCommandPanel';
import { priorityLabel, taskStatusLabel, taskStatusTone } from '../../features/project-management/projectManagementPresentation';
import { toProjectManagementPlatformRoute } from '../../features/project-management/state/projectManagementPlatformRoutes';
import { useProjectManagementWorkspaceScope } from '../../features/project-management/state/projectManagementWorkspaceScope';
import { useMessage } from '../../shared/feedback/useMessage';
import { ResponsivePage } from '../../shared/responsive/ResponsivePage';
import { Page403 } from '../../shared/status/Page403';
import { PageError } from '../../shared/status/PageError';
import { PageLoading } from '../../shared/status/PageLoading';
import { getErrorMessage } from '../../shared/utils/errorMessage';

const categories: Array<{ value: ProjectManagementMyWorkCategory; label: string }> = [
  { value: 'all', label: '全部相关' }, { value: 'assigned', label: '我负责' }, { value: 'participating', label: '我参与' },
  { value: 'created', label: '我创建' }, { value: 'mentioned', label: '提及我' }, { value: 'today', label: '今日到期' },
  { value: 'upcoming', label: '未来 7 天' }, { value: 'overdue', label: '已逾期' }, { value: 'blocked', label: '被阻塞' },
];
const pageSize = 50;
export type MyWorkSortBy = 'dueDate' | 'updated' | 'created' | 'priority';

export function readCategory(value: string | null): ProjectManagementMyWorkCategory {
  return categories.some((category) => category.value === value) ? value as ProjectManagementMyWorkCategory : 'all';
}

export function readPage(value: string | null): number { return Math.max(1, Number(value) || 1); }

export function readSortBy(value: string | null): MyWorkSortBy {
  return value === 'updated' || value === 'created' || value === 'priority' ? value : 'dueDate';
}

export function ProjectManagementMyWorkPage() {
  const scope = useProjectManagementWorkspaceScope();
  const [searchParams, setSearchParams] = useSearchParams();
  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null);
  const message = useMessage();
  const queryClient = useQueryClient();
  const category = readCategory(searchParams.get('category'));
  const pageIndex = readPage(searchParams.get('page'));
  const projectId = searchParams.get('projectId')?.trim() || undefined;
  const sortBy = readSortBy(searchParams.get('sortBy'));
  const query = useMemo(() => ({ category, pageIndex, pageSize, projectId, sortBy, sortDirection: 'asc' as const }), [category, pageIndex, projectId, sortBy]);
  const myWorkQuery = useQuery({ enabled: scope.isAvailable, queryFn: ({ signal }) => getProjectManagementMyWork(query, signal), queryKey: queryKeys.projectManagement.myWork(scope, query) });
  const projectOptionsQuery = useQuery({
    enabled: scope.isAvailable,
    queryFn: ({ signal }) => getProjectManagementMyWorkProjectOptions({ pageIndex: 1, pageSize: 100 }, signal),
    queryKey: [...queryKeys.projectManagement.all(scope), 'my-work-project-options'],
  });
  const updateMutation = useApiMutation({
    mutationFn: ({ id, request }: { id: string; request: ProjectManagementTaskUpsertRequest }) => updateProjectManagementTask(id, request),
    onError: (error) => message.error(getErrorMessage(error, '任务保存失败')),
    onSuccess: async () => {
      message.success('任务已更新');
      setSelectedTaskId(null);
      await queryClient.invalidateQueries({ queryKey: [...queryKeys.projectManagement.all(scope), 'my-work'] });
      await queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.tasksProject(scope, selected?.task.projectId ?? '') });
    },
  });
  const rows = myWorkQuery.data?.data?.items ?? [];
  const selected = rows.find((item) => item.task.id === selectedTaskId) ?? null;
  const total = myWorkQuery.data?.data?.total ?? 0;
  const projectOptions = projectOptionsQuery.data?.data?.items ?? [];
  const setParameter = (name: string, value?: string) => {
    const next = new URLSearchParams(searchParams);
    if (!value) next.delete(name); else next.set(name, value);
    if (name !== 'page') next.delete('page');
    setSearchParams(next, { replace: true });
  };

  if (!scope.isAvailable) return <PageError description="当前会话没有可用的租户和应用工作区" />;
  if (myWorkQuery.isLoading) return <PageLoading />;
  if (myWorkQuery.isError) {
    if (isHttpError(myWorkQuery.error) && myWorkQuery.error.status === 403) return <Page403 />;
    return <PageError action={<button type="button" onClick={() => void myWorkQuery.refetch()}>重试</button>} description="我的工作加载失败" />;
  }
  return (
    <ResponsivePage description="一个受项目权限与 ORM 数据过滤约束的跨项目聚合查询。" eyebrow="ProjectManagement / My Work" title="我的工作">
      <nav className="mb-4 flex flex-wrap gap-2" aria-label="我的工作分类">
        {categories.map((item) => <button className={category === item.value ? 'rounded bg-blue-600 px-3 py-1.5 text-sm text-white' : 'rounded border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50'} key={item.value} onClick={() => setParameter('category', item.value === 'all' ? undefined : item.value)} type="button">{item.label}</button>)}
      </nav>
      <section className="mb-4 flex flex-wrap items-end gap-3 rounded-lg border border-gray-200 bg-white p-3" aria-label="我的工作筛选">
        <label className="text-sm">项目<select aria-label="按项目筛选" className="mt-1 block min-w-56 rounded border border-gray-300 p-2" disabled={projectOptionsQuery.isLoading} onChange={(event) => setParameter('projectId', event.target.value || undefined)} value={projectId ?? ''}><option value="">全部可访问项目</option>{projectId && !projectOptions.some((project) => project.id === projectId) ? <option value={projectId}>当前项目</option> : null}{projectOptions.map((project) => <option key={project.id} value={project.id}>{project.projectCode} · {project.projectName}</option>)}</select></label>
        <label className="text-sm">排序<select className="mt-1 block rounded border border-gray-300 p-2" onChange={(event) => setParameter('sortBy', event.target.value)} value={sortBy}><option value="dueDate">截止日期</option><option value="updated">最近更新</option><option value="created">创建时间</option><option value="priority">优先级</option></select></label>
        {projectOptionsQuery.isError ? <p className="text-sm text-amber-700">项目筛选项加载失败，当前仍可查看全部工作。</p> : null}
      </section>
      <MyWorkTaskCommandPanel item={selected} onCancel={() => setSelectedTaskId(null)} onSubmit={(request) => selected && updateMutation.mutate({ id: selected.task.id, request })} saving={updateMutation.isPending} />
      {rows.length === 0 ? <div className="rounded-lg border border-dashed border-gray-300 p-8 text-center text-sm text-gray-500">暂无匹配任务</div> : <div className="overflow-x-auto rounded-lg border border-gray-200"><table className="min-w-full text-left text-sm"><thead className="bg-gray-50"><tr><th className="px-3 py-2">项目</th><th className="px-3 py-2">任务</th><th className="px-3 py-2">关系</th><th className="px-3 py-2">状态</th><th className="px-3 py-2">优先级</th><th className="px-3 py-2">截止日期</th><th className="px-3 py-2">操作</th></tr></thead><tbody>{rows.map((item) => <tr className="border-t border-gray-100" key={item.task.id}><td className="px-3 py-2">{item.projectName}</td><td className="px-3 py-2"><Link className="text-blue-600 underline" to={`${toProjectManagementPlatformRoute(`projects/${encodeURIComponent(item.task.projectId)}/tasks`)}?taskId=${encodeURIComponent(item.task.id)}`}>{item.task.title}</Link></td><td className="px-3 py-2">{[item.isAssignee && '负责', item.isParticipant && '参与', item.isCreator && '创建', item.isMentioned && '提及'].filter(Boolean).join('、')}</td><td className="px-3 py-2"><span className={`pm-status-badge pm-status-badge--${taskStatusTone(item.task.status)}`}>{taskStatusLabel(item.task.status)}</span></td><td className="px-3 py-2"><span className={`pm-priority-badge pm-priority-badge--${item.task.priority.toLowerCase()}`}>{priorityLabel(item.task.priority)}</span></td><td className="px-3 py-2">{item.task.dueDate ? new Date(item.task.dueDate).toLocaleDateString() : '-'}</td><td className="px-3 py-2"><button type="button" onClick={() => setSelectedTaskId(item.task.id)}>快速更新</button></td></tr>)}</tbody></table></div>}
      <div className="mt-4 flex items-center gap-3 text-sm"><span>共 {total} 项</span><button disabled={pageIndex <= 1} onClick={() => setParameter('page', String(pageIndex - 1))} type="button">上一页</button><span>第 {pageIndex} 页</span><button disabled={pageIndex * pageSize >= total} onClick={() => setParameter('page', String(pageIndex + 1))} type="button">下一页</button></div>
    </ResponsivePage>
  );
}
