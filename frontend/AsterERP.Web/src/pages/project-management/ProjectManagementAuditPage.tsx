import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router-dom';

import { exportProjectManagementAudit, getProjectManagementAudit, getProjectManagementOperations } from '../../api/project-management/projectManagement.api';
import type { ProjectManagementAuditQuery, ProjectManagementOperationQuery } from '../../api/project-management/projectManagement.types';
import { isHttpError } from '../../core/http/httpError';
import { projectManagementQueryKeys } from '../../core/query/projectManagementQueryKeys';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useProjectManagementWorkspaceScope } from '../../features/project-management/state/projectManagementWorkspaceScope';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { PermissionGuard } from '../../shared/auth/PermissionGuard';
import { useMessage } from '../../shared/feedback/useMessage';
import { ResponsivePage } from '../../shared/responsive/ResponsivePage';
import { Page403 } from '../../shared/status/Page403';
import { PageError } from '../../shared/status/PageError';
import { PageLoading } from '../../shared/status/PageLoading';
import { getErrorMessage } from '../../shared/utils/errorMessage';

export function ProjectManagementAuditPage() {
  const scope = useProjectManagementWorkspaceScope();
  const message = useMessage();
  const [keyword, setKeyword] = useState('');
  const [submittedKeyword, setSubmittedKeyword] = useState('');
  const [pageIndex, setPageIndex] = useState(1);
  const pageSize = 100;
  const query: ProjectManagementAuditQuery = { pageIndex, pageSize, keyword: submittedKeyword || undefined };
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
      toolbar={<div className="flex flex-wrap items-center gap-3"><PermissionButton code="project-management:audit:export" disabled={exportMutation.isPending} onClick={() => exportMutation.mutate()}>{exportMutation.isPending ? '导出中…' : '导出 CSV'}</PermissionButton><Link className="text-sm" to="/project-search">项目搜索</Link><PermissionGuard code="project-management:sync:export" fallback={null}><Link className="text-sm" to="/project-sync">同步水位</Link></PermissionGuard></div>}
    >
      <div className="mb-3 flex items-center gap-2">
        <form className="flex items-center gap-2" onSubmit={(event) => { event.preventDefault(); setPageIndex(1); setSubmittedKeyword(keyword.trim()); }}><input className="rounded border border-gray-300 px-3 py-2" aria-label="审计关键字" placeholder="搜索摘要、操作者或对象" value={keyword} onChange={(event) => setKeyword(event.target.value)} /><button type="submit">搜索</button>{submittedKeyword ? <button type="button" onClick={() => { setKeyword(''); setSubmittedKeyword(''); setPageIndex(1); }}>清空</button> : null}</form>
        <span className="text-sm text-gray-500">共 {page.total} 条</span>
      </div>
      <div className="overflow-x-auto rounded-lg border border-gray-200">
        <table className="min-w-full text-left text-sm">
          <thead className="bg-gray-50"><tr><th className="px-3 py-2">时间</th><th className="px-3 py-2">对象</th><th className="px-3 py-2">动作</th><th className="px-3 py-2">摘要</th><th className="px-3 py-2">操作者</th><th className="px-3 py-2">TraceId</th></tr></thead>
          <tbody>{page.items.length === 0 ? <tr><td className="px-3 py-6 text-center text-gray-500" colSpan={6}>{submittedKeyword ? '没有匹配的审计记录' : '暂无审计记录'}</td></tr> : page.items.map((item) => <tr className="border-t border-gray-100" key={item.id}><td className="whitespace-nowrap px-3 py-2">{new Date(item.createdTime).toLocaleString()}</td><td className="px-3 py-2">{item.aggregateType} / {item.aggregateId}</td><td className="px-3 py-2">{item.activityType}</td><td className="max-w-md px-3 py-2">{item.summary ?? '-'}</td><td className="px-3 py-2">{item.actorUserId}</td><td className="px-3 py-2 font-mono text-xs">{item.traceId}</td></tr>)}</tbody>
        </table>
      </div>
      <nav aria-label="审计记录分页" className="mt-3 flex items-center gap-2 text-sm"><button disabled={pageIndex === 1} type="button" onClick={() => setPageIndex((current) => current - 1)}>上一页</button><span>第 {pageIndex} 页</span><button disabled={page.total <= pageIndex * pageSize} type="button" onClick={() => setPageIndex((current) => current + 1)}>下一页</button></nav>
      <section className="mt-5">
        <div className="mb-3 flex items-center gap-2"><h2 className="font-semibold">高风险操作记录</h2><span className="text-sm text-gray-500">共 {operationsQuery.data?.data?.total ?? 0} 条</span></div>
        {operationsQuery.isError ? <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">{isHttpError(operationsQuery.error) && operationsQuery.error.status === 403 ? '无权查看高风险操作记录' : '高风险操作记录暂时无法加载'} <button type="button" className="ml-2 underline" onClick={() => void operationsQuery.refetch()}>重试</button></div> : <div className="overflow-x-auto rounded-lg border border-gray-200"><table className="min-w-full text-left text-sm"><thead className="bg-gray-50"><tr><th className="px-3 py-2">开始时间</th><th className="px-3 py-2">操作</th><th className="px-3 py-2">状态</th><th className="px-3 py-2">失败原因</th><th className="px-3 py-2">操作者</th><th className="px-3 py-2">TraceId</th></tr></thead><tbody>{(operationsQuery.data?.data?.items ?? []).length === 0 ? <tr><td className="px-3 py-6 text-center text-gray-500" colSpan={6}>暂无高风险操作记录</td></tr> : (operationsQuery.data?.data?.items ?? []).map((item) => <tr className="border-t border-gray-100" key={item.id}><td className="whitespace-nowrap px-3 py-2">{new Date(item.startedTime).toLocaleString()}</td><td className="px-3 py-2">{item.operationType}</td><td className="px-3 py-2">{item.status}</td><td className="max-w-md px-3 py-2">{item.errorMessage ?? '-'}</td><td className="px-3 py-2">{item.actorUserId}</td><td className="px-3 py-2 font-mono text-xs">{item.traceId}</td></tr>)}</tbody></table></div>}
      </section>
    </ResponsivePage>
  );
}
