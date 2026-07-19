import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';

import { acknowledgeProjectManagementSync, downloadProjectManagementSyncHistoryReport, getProjectManagementSyncChanges, getProjectManagementSyncHistory, getProjectManagementSyncHistoryDetail, getProjectManagementSyncWatermark } from '../../api/project-management/projectManagement.api';
import { usePermission } from '../../core/auth/usePermission';
import { isHttpError } from '../../core/http/httpError';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useProjectManagementWorkspaceScope } from '../../features/project-management/state/projectManagementWorkspaceScope';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { useMessage } from '../../shared/feedback/useMessage';
import { ResponsivePage } from '../../shared/responsive/ResponsivePage';
import { Page403 } from '../../shared/status/Page403';
import { PageError } from '../../shared/status/PageError';
import { PageLoading } from '../../shared/status/PageLoading';
import { getErrorMessage } from '../../shared/utils/errorMessage';

import { ProjectManagementSyncPackageExportPanel } from './components/ProjectManagementSyncPackageExportPanel';
import { ProjectManagementSyncPackageImportPanel } from './components/ProjectManagementSyncPackageImportPanel';

export function ProjectManagementSyncPage() {
  const scope = useProjectManagementWorkspaceScope();
  const queryClient = useQueryClient();
  const message = useMessage();
  const { hasPermission: canExport } = usePermission('project-management:sync:export');
  const { hasPermission: canImport } = usePermission('project-management:sync:import');
  const [deviceId, setDeviceId] = useState('browser');
  const [submittedDeviceId, setSubmittedDeviceId] = useState('browser');
  const [sinceSequenceNo, setSinceSequenceNo] = useState(0);
  const [selectedHistoryId, setSelectedHistoryId] = useState<string | null>(null);
  const [retryHistoryId, setRetryHistoryId] = useState<string | null>(null);
  const watermarkQuery = useQuery({
    enabled: scope.isAvailable && canExport && Boolean(submittedDeviceId),
    queryKey: ['astererp', 'project-management', scope.tenantId, scope.appCode, 'sync-watermark', submittedDeviceId] as const,
    queryFn: () => getProjectManagementSyncWatermark(submittedDeviceId),
  });
  const changesQuery = useQuery({
    enabled: scope.isAvailable && canExport && Boolean(submittedDeviceId),
    queryKey: ['astererp', 'project-management', scope.tenantId, scope.appCode, 'sync-changes', sinceSequenceNo] as const,
    queryFn: () => getProjectManagementSyncChanges({ sinceSequenceNo, limit: 200 }),
  });
  const historyQuery = useQuery({
    enabled: scope.isAvailable && canImport,
    queryKey: ['astererp', 'project-management', scope.tenantId, scope.appCode, 'sync-history'] as const,
    queryFn: () => getProjectManagementSyncHistory({ pageIndex: 1, pageSize: 100 }),
  });
  const historyDetailQuery = useQuery({
    enabled: scope.isAvailable && canImport && Boolean(selectedHistoryId),
    queryKey: ['astererp', 'project-management', scope.tenantId, scope.appCode, 'sync-history', selectedHistoryId] as const,
    queryFn: () => getProjectManagementSyncHistoryDetail(selectedHistoryId!),
  });
  const acknowledgeMutation = useApiMutation({
    mutationFn: (sequenceNo: number) => acknowledgeProjectManagementSync({ deviceId: submittedDeviceId, sequenceNo }),
    onError: (error) => message.error(getErrorMessage(error, '同步水位确认失败')),
    onSuccess: async () => {
      message.success('同步水位已确认');
      await queryClient.invalidateQueries({ queryKey: ['astererp', 'project-management', scope.tenantId, scope.appCode, 'sync-watermark', submittedDeviceId] });
    },
  });
  const newestSequenceNo = useMemo(() => Math.max(0, ...(changesQuery.data?.data ?? []).map((item) => item.sequenceNo)), [changesQuery.data?.data]);
  const downloadHistoryReport = async (historyId: string) => {
    try {
      const { blob, fileName } = await downloadProjectManagementSyncHistoryReport(historyId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      message.error(getErrorMessage(error, '同步报告下载失败'));
    }
  };

  if (!scope.isAvailable) return <PageError description="当前会话没有可用的租户和应用工作区" />;
  if (!canExport && !canImport) return <Page403 />;
  if (canExport && (watermarkQuery.isLoading || changesQuery.isLoading)) return <PageLoading />;
  if (canExport && (watermarkQuery.isError || changesQuery.isError)) {
    const error = watermarkQuery.error ?? changesQuery.error;
    if (isHttpError(error) && error.status === 403) return <Page403 />;
    return <PageError action={<button type="button" onClick={() => { void watermarkQuery.refetch(); void changesQuery.refetch(); }}>重试</button>} description="同步状态加载失败" />;
  }
  const watermark = watermarkQuery.data?.data;
  const changes = changesQuery.data?.data ?? [];
  const historyItems = historyQuery.data?.data?.items ?? [];
  return <ResponsivePage
    title="项目同步"
    eyebrow="ProjectManagement / Sync"
    description={canExport ? '查看当前工作区的同步水位和变更 journal，并导入同步包；所有确认操作由服务端校验序号。' : '当前账号可导入同步包，但没有查看同步水位和变更记录的权限。'}
  >
    {canExport ? <>
    <section className="rounded-lg border border-gray-200 p-4">
      <form className="flex flex-wrap items-end gap-2" onSubmit={(event) => { event.preventDefault(); setSubmittedDeviceId(deviceId.trim()); }}>
        <label className="text-sm">设备 ID<input className="mt-1" maxLength={120} value={deviceId} onChange={(event) => setDeviceId(event.target.value)} /></label>
        <label className="text-sm">起始序号<input className="mt-1" min={0} type="number" value={sinceSequenceNo} onChange={(event) => setSinceSequenceNo(Math.max(0, Number(event.target.value) || 0))} /></label>
        <button disabled={!deviceId.trim()} type="submit">刷新同步状态</button>
      </form>
      {watermark ? <dl className="mt-4 grid gap-3 sm:grid-cols-3"><div><dt className="text-sm text-gray-500">当前水位</dt><dd className="text-xl font-semibold">{watermark.currentSequenceNo}</dd></div><div><dt className="text-sm text-gray-500">已确认水位</dt><dd className="text-xl font-semibold">{watermark.acknowledgedSequenceNo}</dd></div><div><dt className="text-sm text-gray-500">最近活动</dt><dd>{watermark.lastSeenAt ? new Date(watermark.lastSeenAt).toLocaleString() : '尚未记录'}</dd></div></dl> : <p className="mt-3 text-sm text-gray-500">未返回设备水位。</p>}
      {canImport ? <div className="mt-4 flex flex-wrap gap-2"><PermissionButton code="project-management:sync:import" disabled={!watermark || acknowledgeMutation.isPending} onClick={() => acknowledgeMutation.mutate(watermark?.currentSequenceNo ?? 0)}>确认当前水位</PermissionButton>{newestSequenceNo > 0 ? <PermissionButton code="project-management:sync:import" disabled={acknowledgeMutation.isPending} onClick={() => acknowledgeMutation.mutate(newestSequenceNo)}>确认已加载变更（{newestSequenceNo}）</PermissionButton> : null}</div> : null}
    </section>
    <section className="mt-5 rounded-lg border border-gray-200 p-4">
      <h2 className="font-semibold">导出同步包</h2>
      <p className="mt-1 text-sm text-gray-500">以设备 {submittedDeviceId} 记录导出活动；导出的包仅包含当前服务端授权范围内的项目数据。</p>
      <ProjectManagementSyncPackageExportPanel deviceId={submittedDeviceId} />
    </section>
    <section className="mt-5">
      <div className="mb-2 flex items-center justify-between gap-2"><h2 className="font-semibold">变更记录</h2><span className="text-sm text-gray-500">{changes.length} 条（最多 200 条）</span></div>
      {changes.length === 0 ? <div className="rounded-lg border border-dashed border-gray-300 p-6 text-center text-sm text-gray-500">该水位之后暂无可见变更。</div> : <div className="overflow-x-auto rounded-lg border border-gray-200"><table className="min-w-full text-left text-sm"><thead className="bg-gray-50"><tr><th className="px-3 py-2">序号</th><th className="px-3 py-2">项目</th><th className="px-3 py-2">对象</th><th className="px-3 py-2">操作</th><th className="px-3 py-2">版本</th><th className="px-3 py-2">来源</th><th className="px-3 py-2">字段差异</th><th className="px-3 py-2">时间</th><th className="px-3 py-2">TraceId</th></tr></thead><tbody>{changes.map((item) => <tr className="border-t border-gray-100" key={item.sequenceNo}><td className="px-3 py-2">{item.sequenceNo}</td><td className="px-3 py-2">{item.projectDisplayName ?? '项目已删除或无权查看'}</td><td className="px-3 py-2">{item.aggregateDisplayName ?? '对象已删除或无权查看'}</td><td className="px-3 py-2">{item.operation}</td><td className="px-3 py-2">{item.versionNo}</td><td className="px-3 py-2">{item.source}</td><td className="px-3 py-2">{item.fieldChanges?.length ? item.fieldChanges.map((change) => change.field).join('、') : '未提供'}</td><td className="whitespace-nowrap px-3 py-2">{new Date(item.createdTime).toLocaleString()}</td><td className="px-3 py-2 font-mono text-xs">{item.traceId}</td></tr>)}</tbody></table></div>}
    </section>
    </> : <section className="rounded-lg border border-gray-200 p-4 text-sm text-gray-600">你拥有同步导入权限，但没有同步导出权限；请联系具备同步导出权限的成员生成同步包。</section>}
    {canImport ? <section className="mt-5 rounded-lg border border-gray-200 p-4">
      <div className="flex flex-wrap items-center justify-between gap-2"><div><h2 className="font-semibold">同步历史与导入报告</h2><p className="mt-1 text-sm text-gray-500">记录同步包来源/目标、操作者、计数、TraceId 与错误；下载报告不会包含字段原始值。</p></div><button type="button" onClick={() => { void historyQuery.refetch(); }}>刷新历史</button></div>
      {historyQuery.isLoading ? <p className="mt-3 text-sm text-gray-500">正在加载同步历史…</p> : null}
      {historyQuery.isError ? <p className="mt-3 text-sm text-red-700">同步历史加载失败。</p> : null}
      {!historyQuery.isLoading && !historyQuery.isError && historyItems.length === 0 ? <p className="mt-3 text-sm text-gray-500">尚无同步历史。</p> : null}
      {historyItems.length > 0 ? <div className="mt-3 overflow-x-auto"><table className="min-w-full text-left text-sm"><thead className="bg-gray-50"><tr><th className="px-2 py-2">包 ID / 类型</th><th className="px-2 py-2">来源 → 目标</th><th className="px-2 py-2">时间 / 用户</th><th className="px-2 py-2">状态 / 结果</th><th className="px-2 py-2">TraceId / 错误</th><th className="px-2 py-2">操作</th></tr></thead><tbody>{historyItems.map((item) => <tr className="border-t border-gray-100 align-top" key={item.id}><td className="px-2 py-2"><div className="font-mono text-xs break-all">{item.packageId}</div><div>{item.operationType}</div></td><td className="px-2 py-2">{item.sourceTenantId}/{item.sourceAppCode}{item.sourceDeviceId ? ` (${item.sourceDeviceId})` : ''} → {item.targetTenantId}/{item.targetAppCode}</td><td className="px-2 py-2">{new Date(item.occurredAt).toLocaleString()}<br />{item.actorDisplayName ?? '用户已删除或无权查看'}</td><td className="px-2 py-2"><div>{item.status}</div><div className="text-xs">新增 {item.inserted} / 更新 {item.updated} / 删除 {item.deleted} / 跳过 {item.skipped}<br />冲突 {item.conflictCount} / 失败 {item.failed} / 附件 {item.attachmentsImported}</div></td><td className="px-2 py-2"><div className="font-mono text-xs break-all">{item.traceId}</div>{item.errorMessage ? <div className="mt-1 text-red-700">{item.errorMessage}</div> : null}</td><td className="px-2 py-2"><div className="flex flex-col items-start gap-1"><button type="button" onClick={() => setSelectedHistoryId(item.id)}>展开报告</button><button type="button" onClick={() => { void downloadHistoryReport(item.id); }}>下载脱敏报告</button>{item.operationType === 'Import' && item.status === 'Failed' ? <PermissionButton code="project-management:sync:import" onClick={() => { setRetryHistoryId(item.id); setSelectedHistoryId(item.id); }}>选择安全重试</PermissionButton> : null}</div></td></tr>)}</tbody></table></div> : null}
      {selectedHistoryId ? <div className="mt-4 rounded bg-slate-50 p-3 text-sm"><div className="flex items-center justify-between"><h3 className="font-medium">批次结果与字段冲突</h3><button type="button" onClick={() => setSelectedHistoryId(null)}>收起</button></div>{historyDetailQuery.isLoading ? <p className="mt-2">正在加载报告…</p> : null}{historyDetailQuery.data?.data ? <><p className="mt-2">策略 {historyDetailQuery.data.data.strategy} · 警告 {historyDetailQuery.data.data.warnings.length} · 字段冲突 {historyDetailQuery.data.data.conflicts.length}</p>{historyDetailQuery.data.data.warnings.map((warning) => <p className="mt-1 text-amber-700" key={warning}>{warning}</p>)}{historyDetailQuery.data.data.conflicts.map((conflict, index) => <details className="mt-2 rounded border border-amber-200 p-2" key={`${conflict.aggregateType}-${conflict.aggregateId}-${conflict.field}-${index}`}><summary>冲突对象 · {conflict.field} · 建议 {conflict.recommendedStrategy}</summary><div className="mt-2 break-all text-xs">本地版本 {conflict.localVersionNo ?? '无'}，远端版本 {conflict.remoteVersionNo ?? '无'}<br />本地值：{conflict.localValue ?? '无'}<br />远端值：{conflict.remoteValue ?? '无'}</div></details>)}</> : null}</div> : null}
    </section> : null}
    <section className={canExport ? 'mt-5 rounded-lg border border-gray-200 p-4' : 'rounded-lg border border-gray-200 p-4'}>
      <h2 className="font-semibold">导入同步包</h2>
      <p className="mt-1 text-sm text-gray-500">导入前会校验同步包、展示冲突预览，并要求当前密码和高风险确认。</p>
      <ProjectManagementSyncPackageImportPanel deviceId={submittedDeviceId} retryHistoryId={retryHistoryId} onImportFinished={() => { setRetryHistoryId(null); void historyQuery.refetch(); }} />
    </section>
  </ResponsivePage>;
}
