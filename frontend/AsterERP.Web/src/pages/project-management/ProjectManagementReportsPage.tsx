import { useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';

import { downloadProjectManagementReportSnapshot, exportProjectManagementReportCsv, exportProjectManagementReportExcel, retryProjectManagementReportSnapshot, startProjectManagementReportSnapshot } from '../../api/project-management/projectManagement.api';
import type { ProjectManagementOperation, ProjectManagementReportQuery, ProjectManagementReportSnapshotFormat, ProjectManagementReportSnapshotOptions } from '../../api/project-management/projectManagement.types';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useAuthStore } from '../../core/state/authStore';
import { ProjectManagementOperationProgress } from '../../features/project-management/components/ProjectManagementOperationProgress';
import { getProjectManagementOperationTrackingKey, readProjectManagementOperationTracking, writeProjectManagementOperationTracking } from '../../features/project-management/state/projectManagementOperationTracking';
import { useProjectManagementWorkspaceScope } from '../../features/project-management/state/projectManagementWorkspaceScope';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { useMessage } from '../../shared/feedback/useMessage';
import { ResponsivePage } from '../../shared/responsive/ResponsivePage';
import { getErrorMessage } from '../../shared/utils/errorMessage';

const statuses = ['', 'Planning', 'Active', 'Paused', 'Completed', 'Canceled', 'Archived'];

export function ProjectManagementReportsPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const scope = useProjectManagementWorkspaceScope();
  const userId = useAuthStore((state) => state.user?.userId ?? 'anonymous');
  const message = useMessage();
  const [keyword, setKeyword] = useState('');
  const [status, setStatus] = useState('');
  const [pageSize, setPageSize] = useState(100);
  const [options, setOptions] = useState<ProjectManagementReportSnapshotOptions>({ maxTaskRows: 2000, retentionHours: 24 });
  const [snapshotOperationId, setSnapshotOperationId] = useState<string | null>(null);
  const [completedSnapshot, setCompletedSnapshot] = useState<ProjectManagementOperation | null>(null);
  const snapshotStorageKey = useMemo(() => {
    if (!scope.isAvailable) return null;
    const key = getProjectManagementOperationTrackingKey(scope.tenantId, scope.appCode, userId);
    return key ? `${key}:report-snapshot` : null;
  }, [scope.appCode, scope.isAvailable, scope.tenantId, userId]);
  const query: ProjectManagementReportQuery = { pageIndex: 1, pageSize, keyword: keyword.trim() || undefined, status: status || undefined };

  useEffect(() => {
    setSnapshotOperationId(readProjectManagementOperationTracking(snapshotStorageKey));
    setCompletedSnapshot(null);
  }, [snapshotStorageKey]);

  const csvMutation = useApiMutation({
    mutationFn: () => exportProjectManagementReportCsv(query),
    onError: (error) => message.error(getErrorMessage(error, 'CSV 报表导出失败')),
    onSuccess: (result) => download(result.blob, result.fileName, message),
  });
  const excelMutation = useApiMutation({
    mutationFn: () => exportProjectManagementReportExcel(query),
    onError: (error) => message.error(getErrorMessage(error, 'Excel 报表导出失败')),
    onSuccess: (result) => download(result.blob, result.fileName, message),
  });
  const snapshotMutation = useApiMutation({
    mutationFn: (format: ProjectManagementReportSnapshotFormat) => startProjectManagementReportSnapshot({ format, query, options }),
    onError: (error) => message.error(getErrorMessage(error, '报表快照启动失败')),
    onSuccess: (result) => {
      const operationId = result.data?.operationId;
      if (!operationId) {
        message.error('报表快照未返回后台任务标识');
        return;
      }
      writeProjectManagementOperationTracking(snapshotStorageKey, operationId);
      setCompletedSnapshot(null);
      setSnapshotOperationId(operationId);
      message.success('报表快照已进入后台队列');
    },
  });
  const retryMutation = useApiMutation({
    mutationFn: () => snapshotOperationId ? retryProjectManagementReportSnapshot(snapshotOperationId) : Promise.reject(new Error('报表快照任务不存在')),
    onError: (error) => message.error(getErrorMessage(error, '报表快照重试失败')),
    onSuccess: (result) => {
      const operationId = result.data?.operationId;
      if (!operationId) return message.error('报表快照重试未返回后台任务标识');
      writeProjectManagementOperationTracking(snapshotStorageKey, operationId);
      setCompletedSnapshot(null);
      setSnapshotOperationId(operationId);
      message.success('报表快照已重新进入后台队列');
    },
  });
  const snapshotDownloadMutation = useApiMutation({
    mutationFn: () => snapshotOperationId ? downloadProjectManagementReportSnapshot(snapshotOperationId) : Promise.reject(new Error('报表快照任务不存在')),
    onError: (error) => message.error(getErrorMessage(error, '报表快照下载失败')),
    onSuccess: (result) => download(result.blob, result.fileName, message),
  });
  const isDirectExportPending = csvMutation.isPending || excelMutation.isPending;

  return <ResponsivePage
    title="项目报表"
    eyebrow="ProjectManagement / Reports"
    description="CSV 与 Excel 直接由服务端在当前授权范围内生成；完整 Excel 快照额外包含 Schema、项目、任务、成员、标签、依赖、评论、工时、附件、提醒、活动和变更日志工作表。PDF、CSV、Excel 快照由后台长任务生成并持久化，页面通过 SignalR 和轮询回补显示真实状态。"
    toolbar={<span className="text-sm text-gray-500">当前深链项目：{projectId ?? '未指定'} · 导出范围：当前授权工作区</span>}
  >
    <section className="max-w-4xl rounded-lg border border-gray-200 p-4">
      <h2 className="font-semibold">导出条件</h2>
      <div className="mt-3 grid gap-3 md:grid-cols-3">
        <label className="text-sm">关键字<input className="mt-1 w-full" maxLength={200} placeholder="项目编码或名称" value={keyword} onChange={(event) => setKeyword(event.target.value)} /></label>
        <label className="text-sm">项目状态<select className="mt-1 w-full" value={status} onChange={(event) => setStatus(event.target.value)}>{statuses.map((item) => <option key={item} value={item}>{item || '全部状态'}</option>)}</select></label>
        <label className="text-sm">最大导出行数<select className="mt-1 w-full" value={pageSize} onChange={(event) => setPageSize(Number(event.target.value))}><option value={100}>100</option><option value={500}>500</option></select></label>
      </div>
      <div className="mt-3 flex flex-wrap gap-4 text-sm">
        <label><input type="checkbox" checked={options.includeCompleted ?? false} onChange={(event) => setOptions((current) => ({ ...current, includeCompleted: event.target.checked }))} /> 包含已完成任务</label>
        <label><input type="checkbox" checked={options.includeDeleted ?? false} onChange={(event) => setOptions((current) => ({ ...current, includeDeleted: event.target.checked }))} /> 包含已删除项目</label>
        <label><input type="checkbox" checked={options.includeCommentSummary ?? false} onChange={(event) => setOptions((current) => ({ ...current, includeCommentSummary: event.target.checked }))} /> 评论摘要</label>
        <label><input type="checkbox" checked={options.includeAttachmentList ?? false} onChange={(event) => setOptions((current) => ({ ...current, includeAttachmentList: event.target.checked }))} /> 附件清单</label>
        <label><input type="checkbox" checked={options.includeGanttSnapshot ?? false} onChange={(event) => setOptions((current) => ({ ...current, includeGanttSnapshot: event.target.checked }))} /> 甘特快照</label>
      </div>
      <p className="mt-3 text-sm text-gray-500" role="status">服务端执行行数上限、公式前缀安全处理和数据权限过滤。后台快照完成后可重复下载，不会重新查询当前页面数据。</p>
      <div className="mt-4 flex flex-wrap gap-2">
        <PermissionButton code="project-management:report:export" disabled={isDirectExportPending} onClick={() => csvMutation.mutate()}> {csvMutation.isPending ? 'CSV 导出中…' : '直接导出 CSV'} </PermissionButton>
        <PermissionButton code="project-management:report:export" disabled={isDirectExportPending} onClick={() => excelMutation.mutate()}> {excelMutation.isPending ? 'Excel 导出中…' : '直接导出 Excel'} </PermissionButton>
        <PermissionButton code="project-management:report:export" disabled={snapshotMutation.isPending || Boolean(snapshotOperationId && !completedSnapshot)} onClick={() => snapshotMutation.mutate('pdf')}>{snapshotMutation.isPending ? '正在创建快照…' : '生成 PDF 快照'}</PermissionButton>
        <PermissionButton code="project-management:report:export" disabled={snapshotMutation.isPending || Boolean(snapshotOperationId && !completedSnapshot)} onClick={() => snapshotMutation.mutate('xlsx')}>生成完整 Excel 快照</PermissionButton>
        <PermissionButton code="project-management:report:export" disabled={snapshotMutation.isPending || Boolean(snapshotOperationId && !completedSnapshot)} onClick={() => snapshotMutation.mutate('csv')}>生成 CSV 快照</PermissionButton>
      </div>
    </section>
    {snapshotOperationId ? <section className="mt-4 max-w-4xl rounded-lg border border-sky-200 p-4"><h2 className="font-semibold">后台快照任务</h2><div className="mt-3"><ProjectManagementOperationProgress clearOnTerminal={false} operationId={snapshotOperationId} onTerminal={(operation) => setCompletedSnapshot(operation)} onTrackingEnded={() => { setSnapshotOperationId(null); setCompletedSnapshot(null); }} /></div>{completedSnapshot?.status === 'Succeeded' ? <div className="mt-3 flex flex-wrap items-center gap-3 text-sm text-emerald-800"><span>快照已生成，可在有效期内重复下载。</span><PermissionButton code="project-management:report:export" disabled={snapshotDownloadMutation.isPending} onClick={() => snapshotDownloadMutation.mutate()}>{snapshotDownloadMutation.isPending ? '下载中…' : '下载快照'}</PermissionButton></div> : null}{completedSnapshot?.status === 'Failed' ? <div className="mt-3 flex flex-wrap items-center gap-3 text-sm text-red-700"><span>生成失败：{completedSnapshot.errorMessage ?? '后台服务未提供失败原因'}</span><PermissionButton code="project-management:report:export" disabled={retryMutation.isPending} onClick={() => retryMutation.mutate()}>{retryMutation.isPending ? '重试中…' : '重试生成'}</PermissionButton></div> : null}{completedSnapshot?.status === 'Canceled' ? <div className="mt-3 text-sm text-amber-700">该快照任务已取消，未保留可下载文件。</div> : null}</section> : null}
  </ResponsivePage>;
}

function download(blob: Blob, fileName: string, message: { success: (content: string) => void }) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
  message.success(`已生成 ${fileName}`);
}
