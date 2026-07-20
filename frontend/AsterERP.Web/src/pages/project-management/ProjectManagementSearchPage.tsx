import { useQuery } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { searchProjectManagement } from '../../api/project-management/projectManagement.api';
import type { ProjectManagementSearchItem, ProjectManagementSearchScope } from '../../api/project-management/projectManagement.types';
import { isHttpError } from '../../core/http/httpError';
import { ProjectManagementEscapeStack } from '../../features/project-management/interactions/ProjectManagementEscapeStack';
import { ProjectManagementGlobalSearch } from '../../features/project-management/search/ProjectManagementGlobalSearch';
import { normalizeProjectManagementTargetRoute } from '../../features/project-management/state/projectManagementPlatformRoutes';
import { useProjectManagementWorkspaceScope } from '../../features/project-management/state/projectManagementWorkspaceScope';
import { ResponsivePage } from '../../shared/responsive/ResponsivePage';
import { Page403 } from '../../shared/status/Page403';
import { PageError } from '../../shared/status/PageError';
import { PageLoading } from '../../shared/status/PageLoading';

const scopes: Array<{ label: string; value: ProjectManagementSearchScope }> = [
  { value: 'all', label: '全部' },
  { value: 'projects', label: '项目' },
  { value: 'tasks', label: '任务' },
  { value: 'milestones', label: '里程碑' },
  { value: 'labels', label: '标签' },
  { value: 'members', label: '成员' },
  { value: 'comments', label: '评论' },
];

export function ProjectManagementSearchPage() {
  const scope = useProjectManagementWorkspaceScope();
  const navigate = useNavigate();
  const [keyword, setKeyword] = useState('');
  const [submittedKeyword, setSubmittedKeyword] = useState('');
  const [searchScope, setSearchScope] = useState<ProjectManagementSearchScope>('all');
  const [projectId, setProjectId] = useState('');
  const [status, setStatus] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const query = useMemo(() => ({
    keyword: submittedKeyword,
    scope: searchScope,
    limit: 50,
    projectId: projectId.trim() || undefined,
    status: status || undefined,
    from: from || undefined,
    to: to || undefined,
    pageIndex: 1,
  }), [from, projectId, searchScope, status, submittedKeyword, to]);
  const searchQuery = useQuery({
    enabled: scope.isAvailable && Boolean(submittedKeyword),
    queryKey: ['astererp', 'project-management', scope.tenantId, scope.appCode, 'search', query] as const,
    queryFn: ({ signal }) => searchProjectManagement(query, signal),
  });

  if (!scope.isAvailable) return <PageError description="当前会话没有可用的租户和应用工作区" />;
  if (searchQuery.isLoading) return <PageLoading />;
  if (searchQuery.isError) {
    if (isHttpError(searchQuery.error) && searchQuery.error.status === 403) return <Page403 />;
    return <PageError action={<button type="button" onClick={() => void searchQuery.refetch()}>重试</button>} description="项目域搜索失败" />;
  }

  const result = searchQuery.data?.data;
  const total = (result?.projects.length ?? 0) + (result?.tasks.length ?? 0) + (result?.milestones.length ?? 0) + (result?.labels.length ?? 0) + (result?.members.length ?? 0) + (result?.comments.length ?? 0);
  return (
    <ProjectManagementEscapeStack>
    <ResponsivePage
      title="项目域搜索"
      eyebrow="ProjectManagement / Search"
      description="搜索结果由当前工作区的服务端权限和数据过滤决定；点击结果后仍会在目标页面重新校验访问权限。"
      toolbar={
        <div className="flex flex-wrap items-center gap-2">
          <ProjectManagementGlobalSearch />
          <form className="flex flex-wrap items-center gap-2" onSubmit={(event) => { event.preventDefault(); setSubmittedKeyword(keyword.trim()); }}>
            <input aria-label="搜索关键字" className="min-w-64" maxLength={200} placeholder="搜索项目、任务、成员或评论" value={keyword} onChange={(event) => setKeyword(event.target.value)} />
            <select aria-label="搜索范围" value={searchScope} onChange={(event) => setSearchScope(event.target.value as ProjectManagementSearchScope)}>{scopes.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}</select>
            <input aria-label="按项目筛选" placeholder="项目编码或名称（可选）" value={projectId} onChange={(event) => setProjectId(event.target.value)} />
            <input aria-label="按状态筛选" placeholder="状态（可选）" value={status} onChange={(event) => setStatus(event.target.value)} />
            <label className="text-sm">从 <input aria-label="开始时间" type="date" value={from} onChange={(event) => setFrom(event.target.value)} /></label>
            <label className="text-sm">至 <input aria-label="结束时间" type="date" value={to} onChange={(event) => setTo(event.target.value)} /></label>
            <button disabled={!keyword.trim()} type="submit">搜索</button>
            {submittedKeyword || projectId || status || from || to ? <button type="button" onClick={() => { setKeyword(''); setSubmittedKeyword(''); setProjectId(''); setStatus(''); setFrom(''); setTo(''); }}>清空</button> : null}
          </form>
        </div>
      }
    >
      {!submittedKeyword ? <SearchEmptyState /> : <div className="space-y-5" aria-live="polite">
        <p className="text-sm text-gray-500">“{submittedKeyword}”共找到 {total} 条结果。</p>
        <SearchResultGroup title="项目" emptyText="没有匹配的项目" items={result?.projects ?? []} onOpen={(item) => navigate(normalizeProjectManagementTargetRoute(item.targetRoute))} />
        <SearchResultGroup title="任务" emptyText="没有匹配的任务" items={result?.tasks ?? []} onOpen={(item) => navigate(normalizeProjectManagementTargetRoute(item.targetRoute))} />
        <SearchResultGroup title="里程碑" emptyText="没有匹配的里程碑" items={result?.milestones ?? []} onOpen={(item) => navigate(normalizeProjectManagementTargetRoute(item.targetRoute))} />
        <SearchResultGroup title="标签" emptyText="没有匹配的标签" items={result?.labels ?? []} onOpen={(item) => navigate(normalizeProjectManagementTargetRoute(item.targetRoute))} />
        <SearchResultGroup title="成员" emptyText="没有匹配的成员" items={result?.members ?? []} onOpen={(item) => navigate(normalizeProjectManagementTargetRoute(item.targetRoute))} />
        <SearchResultGroup title="评论" emptyText="没有匹配的评论" items={result?.comments ?? []} onOpen={(item) => navigate(normalizeProjectManagementTargetRoute(item.targetRoute))} />
      </div>}
    </ResponsivePage>
    </ProjectManagementEscapeStack>
  );
}

function SearchEmptyState() {
  return <div className="rounded-lg border border-dashed border-gray-300 p-8 text-center text-sm text-gray-500">输入至少一个关键字后开始搜索。项目、任务、里程碑、标签、成员和评论都会由服务端在当前授权范围内检索。</div>;
}

function SearchResultGroup({ emptyText, items, onOpen, title }: { emptyText: string; items: ProjectManagementSearchItem[]; onOpen: (item: ProjectManagementSearchItem) => void; title: string }) {
  return <section aria-label={title}>
    <h2 className="mb-2 font-semibold">{title}（{items.length}）</h2>
    {items.length === 0 ? <div className="rounded-lg border border-dashed border-gray-300 p-4 text-sm text-gray-500">{emptyText}</div> : <div className="overflow-x-auto rounded-lg border border-gray-200"><table className="min-w-full text-left text-sm"><thead className="bg-gray-50"><tr><th scope="col" className="px-3 py-2">标题</th><th scope="col" className="px-3 py-2">摘要</th><th scope="col" className="px-3 py-2">最近更新</th><th scope="col" className="px-3 py-2">操作</th></tr></thead><tbody>{items.map((item) => <tr className="border-t border-gray-100" key={`${item.resultType}-${item.id}`}><td className="px-3 py-2 font-medium">{item.title}</td><td className="max-w-xl px-3 py-2">{item.summary ?? '-'}</td><td className="whitespace-nowrap px-3 py-2">{item.updatedTime ? new Date(item.updatedTime).toLocaleString() : '-'}</td><td className="px-3 py-2"><button type="button" aria-label={`打开${title}结果：${item.title}`} onClick={() => onOpen(item)}>打开</button></td></tr>)}</tbody></table></div>}
  </section>;
}
