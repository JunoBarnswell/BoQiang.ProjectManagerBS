import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';

import { exportProjectManagementAudit, getProjectManagementAudit, getProjectManagementAuditDetail, getProjectManagementOperations, startProjectManagementWorkspaceValidation } from '../../api/project-management/projectManagement.api';
import type { ProjectManagementAuditQuery, ProjectManagementOperationQuery } from '../../api/project-management/projectManagement.types';
import { isHttpError } from '../../core/http/httpError';
import { projectManagementQueryKeys } from '../../core/query/projectManagementQueryKeys';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useAuthStore } from '../../core/state/authStore';
import { ProjectManagementOperationProgress } from '../../features/project-management/components/ProjectManagementOperationProgress';
import { clearProjectManagementOperationTracking, getProjectManagementOperationTrackingKey, readProjectManagementOperationTracking, writeProjectManagementOperationTracking } from '../../features/project-management/state/projectManagementOperationTracking';
import { toProjectManagementPlatformRoute } from '../../features/project-management/state/projectManagementPlatformRoutes';
import { useProjectManagementWorkspaceScope } from '../../features/project-management/state/projectManagementWorkspaceScope';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { PermissionGuard } from '../../shared/auth/PermissionGuard';
import { useMessage } from '../../shared/feedback/useMessage';
import { ResponsivePage } from '../../shared/responsive/ResponsivePage';
import { Page403 } from '../../shared/status/Page403';
import { PageError } from '../../shared/status/PageError';
import { PageLoading } from '../../shared/status/PageLoading';
import { getErrorMessage } from '../../shared/utils/errorMessage';

import { ProjectManagementAuditDetailDrawer } from './components/ProjectManagementAuditDetailDrawer';

function optional(value: string): string | undefined {
  const normalized = value.trim();
  return normalized || undefined;
}

function toIso(value: string): string | undefined {
  if (!value) return undefined;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? undefined : date.toISOString();
}

function hasAuditFilters(filters: Record<string, string>): boolean {
  return Object.values(filters).some((value) => value.trim().length > 0);
}

export function ProjectManagementAuditPage() {
  const scope = useProjectManagementWorkspaceScope();
  const userId = useAuthStore((state) => state.user?.userId ?? 'anonymous');
  const queryClient = useQueryClient();
  const message = useMessage();
  const [filters, setFilters] = useState({
    keyword: '', projectId: '', aggregateType: '', activityType: '', actorUserId: '', actorRole: '', source: '', sourceDeviceId: '', from: '', to: '', result: '',
  });
  const [submittedFilters, setSubmittedFilters] = useState(filters);
  const [sort, setSort] = useState<{ field: 'createdTime' | 'projectId' | 'aggregateType' | 'activityType' | 'actorUserId'; order: 'asc' | 'desc' }>({ field: 'createdTime', order: 'desc' });
  const [pageIndex, setPageIndex] = useState(1);
  const [operationId, setOperationId] = useState<string | null>(null);
  const [selectedAuditId, setSelectedAuditId] = useState<string | null>(null);
  const operationStorageKey = useMemo(
    () => scope.isAvailable ? getProjectManagementOperationTrackingKey(scope.tenantId, scope.appCode, userId) : null,
    [scope.appCode, scope.isAvailable, scope.tenantId, userId],
  );
  const pageSize = 100;
  const query: ProjectManagementAuditQuery = {
    pageIndex,
    pageSize,
    keyword: optional(submittedFilters.keyword),
    projectId: optional(submittedFilters.projectId),
    aggregateType: optional(submittedFilters.aggregateType),
    activityType: optional(submittedFilters.activityType),
    actorUserId: optional(submittedFilters.actorUserId),
    actorRole: optional(submittedFilters.actorRole),
    source: optional(submittedFilters.source),
    sourceDeviceId: optional(submittedFilters.sourceDeviceId),
    from: toIso(submittedFilters.from),
    to: toIso(submittedFilters.to),
    isSuccess: submittedFilters.result === 'succeeded' ? true : submittedFilters.result === 'failed' ? false : undefined,
    sorts: [sort],
  };
  const auditQuery = useQuery({
    enabled: scope.isAvailable,
    queryKey: projectManagementQueryKeys.audit(scope, query),
    queryFn: () => getProjectManagementAudit(query),
  });
  const operationQuery: ProjectManagementOperationQuery = { pageIndex: 1, pageSize: 100 };
  const operationsQuery = useQuery({
    enabled: scope.isAvailable,
    queryKey: projectManagementQueryKeys.operations(scope, operationQuery),
    queryFn: () => getProjectManagementOperations(operationQuery),
  });
  const auditDetailQuery = useQuery({
    enabled: scope.isAvailable && selectedAuditId !== null,
    queryKey: projectManagementQueryKeys.auditDetail(scope, selectedAuditId ?? 'none'),
    queryFn: () => getProjectManagementAuditDetail(selectedAuditId!),
  });

  useEffect(() => {
    if (!operationStorageKey) {
      setOperationId(null);
      return;
    }
    setOperationId(readProjectManagementOperationTracking(operationStorageKey));
  }, [operationStorageKey]);

  const exportMutation = useApiMutation({
    mutationFn: () => exportProjectManagementAudit(query),
    onError: (error) => message.error(getErrorMessage(error, '审计记录导出失败')),
    onSuccess: (result) => {
      const url = URL.createObjectURL(result.blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = result.fileName;
      anchor.click();
      URL.revokeObjectURL(url);
      message.success('审计记录已导出');
    }
  });
  const workspaceValidationMutation = useApiMutation({
    mutationFn: startProjectManagementWorkspaceValidation,
    onError: (error) => message.error(getErrorMessage(error, '工作区校验失败')),
    onSuccess: (result) => {
      const operation = result.data;
      if (!operation) return;
      setOperationId(operation.id);
      writeProjectManagementOperationTracking(operationStorageKey, operation.id);
      void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.operations(scope, operationQuery) });
      message.success('工作区校验已启动，正在后台执行');
    }
  });

  if (auditQuery.isLoading) return <PageLoading />;
  if (auditQuery.isError) {
    if (isHttpError(auditQuery.error) && auditQuery.error.status === 403) return <Page403 />;
    return <PageError action={<button type="button" onClick={() => void auditQuery.refetch()}>重试</button>} description="审计记录加载失败" />;
  }
  const page = auditQuery.data?.data;
  if (!page) return <PageError description="审计记录为空" />;

  return (
    <ResponsivePage
      title="项目审计中心"
      eyebrow="ProjectManagement / Audit"
      description="查看当前授权范围内的项目、里程碑和任务活动；导出结果沿用同一数据权限过滤。"
      toolbar={<div className="flex flex-wrap items-center gap-3"><PermissionButton code="project-management:audit:export" disabled={exportMutation.isPending} onClick={() => exportMutation.mutate()}>{exportMutation.isPending ? '导出中…' : '导出 CSV'}</PermissionButton><Link className="text-sm" to={toProjectManagementPlatformRoute('project-search')}>项目搜索</Link><PermissionGuard code="project-management:sync:export" fallback={null}><Link className="text-sm" to={toProjectManagementPlatformRoute('project-sync')}>同步水位</Link></PermissionGuard></div>}
    >
      <div className="mb-3 flex items-center gap-2">
        <form className="flex flex-wrap items-center gap-2" onSubmit={(event) => { event.preventDefault(); setPageIndex(1); setSubmittedFilters(filters); }}>
          <input className="rounded border border-gray-300 px-3 py-2" aria-label="审计关键字" placeholder="摘要、操作者或对象" value={filters.keyword} onChange={(event) => setFilters((current) => ({ ...current, keyword: event.target.value }))} />
          <input className="rounded border border-gray-300 px-3 py-2" aria-label="项目标识" placeholder="项目 ID" value={filters.projectId} onChange={(event) => setFilters((current) => ({ ...current, projectId: event.target.value }))} />
          <input className="rounded border border-gray-300 px-3 py-2" aria-label="实体类型" placeholder="实体类型" value={filters.aggregateType} onChange={(event) => setFilters((current) => ({ ...current, aggregateType: event.target.value }))} />
          <input className="rounded border border-gray-300 px-3 py-2" aria-label="操作类型" placeholder="操作类型" value={filters.activityType} onChange={(event) => setFilters((current) => ({ ...current, activityType: event.target.value }))} />
          <input className="rounded border border-gray-300 px-3 py-2" aria-label="操作者" placeholder="操作者 ID" value={filters.actorUserId} onChange={(event) => setFilters((current) => ({ ...current, actorUserId: event.target.value }))} />
          <input className="rounded border border-gray-300 px-3 py-2" aria-label="项目角色" placeholder="项目角色" value={filters.actorRole} onChange={(event) => setFilters((current) => ({ ...current, actorRole: event.target.value }))} />
          <input className="rounded border border-gray-300 px-3 py-2" aria-label="来源方式" placeholder="来源方式" value={filters.source} onChange={(event) => setFilters((current) => ({ ...current, source: event.target.value }))} />
          <input className="rounded border border-gray-300 px-3 py-2" aria-label="来源设备" placeholder="来源设备 ID" value={filters.sourceDeviceId} onChange={(event) => setFilters((current) => ({ ...current, sourceDeviceId: event.target.value }))} />
          <select className="rounded border border-gray-300 px-3 py-2" aria-label="结果" value={filters.result} onChange={(event) => setFilters((current) => ({ ...current, result: event.target.value }))}><option value="">全部结果</option><option value="succeeded">成功</option><option value="failed">失败</option></select>
          <label className="text-sm">从 <input className="rounded border border-gray-300 px-2 py-1" aria-label="开始时间" type="datetime-local" value={filters.from} onChange={(event) => setFilters((current) => ({ ...current, from: event.target.value }))} /></label>
          <label className="text-sm">至 <input className="rounded border border-gray-300 px-2 py-1" aria-label="结束时间" type="datetime-local" value={filters.to} onChange={(event) => setFilters((current) => ({ ...current, to: event.target.value }))} /></label>
          <select className="rounded border border-gray-300 px-3 py-2" aria-label="排序字段" value={sort.field} onChange={(event) => setSort((current) => ({ ...current, field: event.target.value as typeof current.field }))}><option value="createdTime">时间</option><option value="projectId">项目</option><option value="aggregateType">实体类型</option><option value="activityType">操作</option><option value="actorUserId">操作者</option></select>
          <select className="rounded border border-gray-300 px-3 py-2" aria-label="排序方向" value={sort.order} onChange={(event) => setSort((current) => ({ ...current, order: event.target.value as typeof current.order }))}><option value="desc">降序</option><option value="asc">升序</option></select>
          <button type="submit">搜索</button>
          <button type="button" onClick={() => { const next = { keyword: '', projectId: '', aggregateType: '', activityType: '', actorUserId: '', actorRole: '', source: '', sourceDeviceId: '', from: '', to: '', result: '' }; setFilters(next); setSubmittedFilters(next); setPageIndex(1); }}>清空</button>
        </form>
        <span className="text-sm text-gray-500">共 {page.total} 条</span>
      </div>
      <div className="overflow-x-auto rounded-lg border border-gray-200">
        <table className="min-w-full text-left text-sm">
          <thead className="bg-gray-50"><tr><th className="px-3 py-2">时间</th><th className="px-3 py-2">项目</th><th className="px-3 py-2">对象</th><th className="px-3 py-2">动作</th><th className="px-3 py-2">来源 / 设备</th><th className="px-3 py-2">结果</th><th className="px-3 py-2">摘要</th><th className="px-3 py-2">操作者</th><th className="px-3 py-2">TraceId</th><th className="px-3 py-2">详情</th></tr></thead>
          <tbody>{page.items.length === 0 ? <tr><td className="px-3 py-6 text-center text-gray-500" colSpan={10}>{hasAuditFilters(submittedFilters) ? '没有匹配的审计记录' : '暂无审计记录'}</td></tr> : page.items.map((item) => <tr className="border-t border-gray-100" key={item.id}><td className="whitespace-nowrap px-3 py-2">{new Date(item.createdTime).toLocaleString()}</td><td className="px-3 py-2 font-mono text-xs">{item.projectId}</td><td className="px-3 py-2">{item.aggregateType} / {item.aggregateId}</td><td className="px-3 py-2">{item.activityType}</td><td className="px-3 py-2">{item.source}{item.sourceDeviceId ? ` / ${item.sourceDeviceId}` : ''}</td><td className="px-3 py-2">{item.isSuccess ? '成功' : '失败'}</td><td className="max-w-md px-3 py-2">{item.summary ?? '-'}</td><td className="px-3 py-2">{item.actorUserId}</td><td className="px-3 py-2 font-mono text-xs">{item.traceId}</td><td className="px-3 py-2"><button className="underline" type="button" onClick={() => setSelectedAuditId(item.id)}>查看</button></td></tr>)}</tbody>
        </table>
      </div>
      <nav aria-label="审计记录分页" className="mt-3 flex items-center gap-2 text-sm"><button disabled={pageIndex === 1} type="button" onClick={() => setPageIndex((current) => current - 1)}>上一页</button><span>第 {pageIndex} 页</span><button disabled={page.total <= pageIndex * pageSize} type="button" onClick={() => setPageIndex((current) => current + 1)}>下一页</button></nav>
      <section className="mt-5">
        <div className="mb-3 flex flex-wrap items-center gap-2"><h2 className="font-semibold">高风险操作记录</h2><span className="text-sm text-gray-500">共 {operationsQuery.data?.data?.total ?? 0} 条</span><PermissionButton code="project-management:operation:manage" disabled={workspaceValidationMutation.isPending} onClick={() => workspaceValidationMutation.mutate()}>{workspaceValidationMutation.isPending ? '校验中…' : '运行工作区校验'}</PermissionButton></div>
        {operationId ? <div className="mb-3"><ProjectManagementOperationProgress operationId={operationId} onChanged={() => void operationsQuery.refetch()} onTrackingEnded={() => { clearProjectManagementOperationTracking(operationStorageKey); setOperationId(null); void operationsQuery.refetch(); }} /></div> : null}
        {operationsQuery.isError ? <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">{isHttpError(operationsQuery.error) && operationsQuery.error.status === 403 ? '无权查看高风险操作记录' : '高风险操作记录暂时无法加载'} <button type="button" className="ml-2 underline" onClick={() => void operationsQuery.refetch()}>重试</button></div> : <div className="overflow-x-auto rounded-lg border border-gray-200"><table className="min-w-full text-left text-sm"><thead className="bg-gray-50"><tr><th className="px-3 py-2">开始时间</th><th className="px-3 py-2">操作</th><th className="px-3 py-2">状态</th><th className="px-3 py-2">失败原因</th><th className="px-3 py-2">操作者</th><th className="px-3 py-2">TraceId</th><th className="px-3 py-2">跟踪</th></tr></thead><tbody>{(operationsQuery.data?.data?.items ?? []).length === 0 ? <tr><td className="px-3 py-6 text-center text-gray-500" colSpan={7}>暂无高风险操作记录</td></tr> : (operationsQuery.data?.data?.items ?? []).map((item) => <tr className="border-t border-gray-100" key={item.id}><td className="whitespace-nowrap px-3 py-2">{new Date(item.startedTime).toLocaleString()}</td><td className="px-3 py-2">{item.operationType}</td><td className="px-3 py-2">{item.status}</td><td className="max-w-md px-3 py-2">{item.errorMessage ?? '-'}</td><td className="px-3 py-2">{item.actorUserId}</td><td className="px-3 py-2 font-mono text-xs">{item.traceId}</td><td className="px-3 py-2"><button type="button" className="underline" onClick={() => { setOperationId(item.id); writeProjectManagementOperationTracking(operationStorageKey, item.id); }}>继续跟踪</button></td></tr>)}</tbody></table></div>}
      </section>
      <ProjectManagementAuditDetailDrawer detail={auditDetailQuery.data?.data} error={auditDetailQuery.isError} loading={auditDetailQuery.isLoading} open={selectedAuditId !== null} onClose={() => setSelectedAuditId(null)} onRetry={() => void auditDetailQuery.refetch()} />
    </ResponsivePage>
  );
}
