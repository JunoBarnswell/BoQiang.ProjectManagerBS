import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';

import { acknowledgeProjectManagementSync, getProjectManagementSyncChanges, getProjectManagementSyncWatermark } from '../../api/project-management/projectManagement.api';
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

export function ProjectManagementSyncPage() {
  const scope = useProjectManagementWorkspaceScope();
  const queryClient = useQueryClient();
  const message = useMessage();
  const [deviceId, setDeviceId] = useState('browser');
  const [submittedDeviceId, setSubmittedDeviceId] = useState('browser');
  const [sinceSequenceNo, setSinceSequenceNo] = useState(0);
  const watermarkQuery = useQuery({
    enabled: scope.isAvailable && Boolean(submittedDeviceId),
    queryKey: ['astererp', 'project-management', scope.tenantId, scope.appCode, 'sync-watermark', submittedDeviceId] as const,
    queryFn: () => getProjectManagementSyncWatermark(submittedDeviceId),
  });
  const changesQuery = useQuery({
    enabled: scope.isAvailable && Boolean(submittedDeviceId),
    queryKey: ['astererp', 'project-management', scope.tenantId, scope.appCode, 'sync-changes', sinceSequenceNo] as const,
    queryFn: () => getProjectManagementSyncChanges({ sinceSequenceNo, limit: 200 }),
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

  if (!scope.isAvailable) return <PageError description="当前会话没有可用的租户和应用工作区" />;
  if (watermarkQuery.isLoading || changesQuery.isLoading) return <PageLoading />;
  if (watermarkQuery.isError || changesQuery.isError) {
    const error = watermarkQuery.error ?? changesQuery.error;
    if (isHttpError(error) && error.status === 403) return <Page403 />;
    return <PageError action={<button type="button" onClick={() => { void watermarkQuery.refetch(); void changesQuery.refetch(); }}>重试</button>} description="同步状态加载失败" />;
  }
  const watermark = watermarkQuery.data?.data;
  const changes = changesQuery.data?.data ?? [];
  return <ResponsivePage
    title="项目同步"
    eyebrow="ProjectManagement / Sync"
    description="查看当前工作区的同步水位和变更 journal；导入/导出同步包仍在数据空间执行，所有确认操作由服务端校验序号。"
    toolbar={<Link to="/project-data-space">前往数据空间导入或导出同步包</Link>}
  >
    <section className="rounded-lg border border-gray-200 p-4">
      <form className="flex flex-wrap items-end gap-2" onSubmit={(event) => { event.preventDefault(); setSubmittedDeviceId(deviceId.trim()); }}>
        <label className="text-sm">设备 ID<input className="mt-1" maxLength={120} value={deviceId} onChange={(event) => setDeviceId(event.target.value)} /></label>
        <label className="text-sm">起始序号<input className="mt-1" min={0} type="number" value={sinceSequenceNo} onChange={(event) => setSinceSequenceNo(Math.max(0, Number(event.target.value) || 0))} /></label>
        <button disabled={!deviceId.trim()} type="submit">刷新同步状态</button>
      </form>
      {watermark ? <dl className="mt-4 grid gap-3 sm:grid-cols-3"><div><dt className="text-sm text-gray-500">当前水位</dt><dd className="text-xl font-semibold">{watermark.currentSequenceNo}</dd></div><div><dt className="text-sm text-gray-500">已确认水位</dt><dd className="text-xl font-semibold">{watermark.acknowledgedSequenceNo}</dd></div><div><dt className="text-sm text-gray-500">最近活动</dt><dd>{watermark.lastSeenAt ? new Date(watermark.lastSeenAt).toLocaleString() : '尚未记录'}</dd></div></dl> : <p className="mt-3 text-sm text-gray-500">未返回设备水位。</p>}
      <div className="mt-4 flex flex-wrap gap-2"><PermissionButton code="project-management:sync:import" disabled={!watermark || acknowledgeMutation.isPending} onClick={() => acknowledgeMutation.mutate(watermark?.currentSequenceNo ?? 0)}>确认当前水位</PermissionButton>{newestSequenceNo > 0 ? <PermissionButton code="project-management:sync:import" disabled={acknowledgeMutation.isPending} onClick={() => acknowledgeMutation.mutate(newestSequenceNo)}>确认已加载变更（{newestSequenceNo}）</PermissionButton> : null}</div>
    </section>
    <section className="mt-5">
      <div className="mb-2 flex items-center justify-between gap-2"><h2 className="font-semibold">变更记录</h2><span className="text-sm text-gray-500">{changes.length} 条（最多 200 条）</span></div>
      {changes.length === 0 ? <div className="rounded-lg border border-dashed border-gray-300 p-6 text-center text-sm text-gray-500">该水位之后暂无可见变更。</div> : <div className="overflow-x-auto rounded-lg border border-gray-200"><table className="min-w-full text-left text-sm"><thead className="bg-gray-50"><tr><th className="px-3 py-2">序号</th><th className="px-3 py-2">对象</th><th className="px-3 py-2">操作</th><th className="px-3 py-2">版本</th><th className="px-3 py-2">时间</th><th className="px-3 py-2">TraceId</th></tr></thead><tbody>{changes.map((item) => <tr className="border-t border-gray-100" key={item.sequenceNo}><td className="px-3 py-2">{item.sequenceNo}</td><td className="px-3 py-2">{item.aggregateType} / {item.aggregateId}</td><td className="px-3 py-2">{item.operation}</td><td className="px-3 py-2">{item.versionNo}</td><td className="whitespace-nowrap px-3 py-2">{new Date(item.createdTime).toLocaleString()}</td><td className="px-3 py-2 font-mono text-xs">{item.traceId}</td></tr>)}</tbody></table></div>}
    </section>
  </ResponsivePage>;
}
