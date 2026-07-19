import { useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';

import { downloadProjectManagementReportSnapshot, exportProjectManagementReportCsv, exportProjectManagementReportExcel, retryProjectManagementReportSnapshot, startProjectManagementReportSnapshot } from '../../api/project-management/projectManagement.api';
import type { ProjectManagementOperation, ProjectManagementReportQuery, ProjectManagementReportSnapshotFormat, ProjectManagementReportSnapshotOptions } from '../../api/project-management/projectManagement.types';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useAuthStore } from '../../core/state/authStore';
import { projectStatusLabel } from '../../features/project-management/projectManagementPresentation';
import { ProjectManagementSnapshotJobStatus } from '../../features/project-management/reports/ProjectManagementSnapshotJobStatus';
import { getProjectManagementOperationTrackingKey, readProjectManagementOperationTracking, writeProjectManagementOperationTracking } from '../../features/project-management/state/projectManagementOperationTracking';
import { useProjectManagementWorkspaceScope } from '../../features/project-management/state/projectManagementWorkspaceScope';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { useMessage } from '../../shared/feedback/useMessage';
import { saveBlob } from '../../shared/file-preview/filePreviewUtils';
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

  const saveDownload = (blob: Blob, fileName: string) => {
    saveBlob(blob, fileName);
    message.success(`已生成 ${fileName}`);
  };
  const csvMutation = useApiMutation({
    mutationFn: () => exportProjectManagementReportCsv(query),
    onError: (error) => message.error(getErrorMessage(error, 'CSV 报表导出失败')),
    onSuccess: (result) => saveDownload(result.blob, result.fileName),
  });
  const excelMutation = useApiMutation({
    mutationFn: () => exportProjectManagementReportExcel(query),
    onError: (error) => message.error(getErrorMessage(error, 'Excel 报表导出失败')),
    onSuccess: (result) => saveDownload(result.blob, result.fileName),
  });
  const snapshotMutation = useApiMutation({
    mutationFn: (format: ProjectManagementReportSnapshotFormat) => startProjectManagementReportSnapshot({ format, query, options }),
    onError: (error) => message.error(getErrorMessage(error, '报表快照启动失败')),
    onSuccess: (result) => {
      const operationId = result.data?.operationId;
      if (!operationId) return message.error('报表快照未返回后台任务标识');
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
    onSuccess: (result) => saveDownload(result.blob, result.fileName),
  });
  const isDirectExportPending = csvMutation.isPending || excelMutation.isPending;
  const canStartSnapshot = !snapshotMutation.isPending && !snapshotOperationId;

  return (
    <ResponsivePage
      title="项目报表"
      eyebrow="ProjectManagement / Reports"
      description="直接导出以当前授权范围实时生成；后台快照持久化生成过程和下载结果。"
      toolbar={<span className="text-sm text-gray-500">当前深链项目：{projectId ?? '未指定'} · 导出范围：当前授权工作区</span>}
    >
      <section className="max-w-5xl rounded-lg border border-gray-200 p-4" aria-labelledby="report-conditions-title">
        <h2 id="report-conditions-title" className="font-semibold">条件设置</h2>
        <div className="mt-3 grid gap-3 md:grid-cols-3">
          <label className="text-sm">关键字<input className="mt-1 w-full" maxLength={200} placeholder="项目编码或名称" value={keyword} onChange={(event) => setKeyword(event.target.value)} /></label>
          <label className="text-sm">项目状态<select className="mt-1 w-full" value={status} onChange={(event) => setStatus(event.target.value)}>{statuses.map((item) => <option key={item} value={item}>{item ? projectStatusLabel(item) : '全部状态'}</option>)}</select></label>
          <label className="text-sm">导出范围<select className="mt-1 w-full" value={pageSize} onChange={(event) => setPageSize(Number(event.target.value))}><option value={100}>标准导出（最多 100 项）</option><option value={500}>完整导出（最多 500 项）</option></select></label>
        </div>
        <div className="mt-4 grid gap-4 md:grid-cols-2">
          <OptionGroup title="基础选项">
            <Checkbox checked={options.includeCompleted ?? false} label="包含已完成任务" onChange={(checked) => setOptions((current) => ({ ...current, includeCompleted: checked }))} />
            <Checkbox checked={options.includeDeleted ?? false} label="包含已删除项目" onChange={(checked) => setOptions((current) => ({ ...current, includeDeleted: checked }))} />
          </OptionGroup>
          <OptionGroup title="附加内容">
            <Checkbox checked={options.includeCommentSummary ?? false} label="评论摘要" onChange={(checked) => setOptions((current) => ({ ...current, includeCommentSummary: checked }))} />
            <Checkbox checked={options.includeAttachmentList ?? false} label="附件清单" onChange={(checked) => setOptions((current) => ({ ...current, includeAttachmentList: checked }))} />
            <Checkbox checked={options.includeGanttSnapshot ?? false} label="甘特快照" onChange={(checked) => setOptions((current) => ({ ...current, includeGanttSnapshot: checked }))} />
          </OptionGroup>
        </div>
        <p className="mt-3 text-sm text-gray-500" role="status">服务端执行行数上限、公式前缀安全处理和数据权限过滤。</p>
      </section>
      <section className="mt-4 grid max-w-5xl gap-4 lg:grid-cols-2" aria-label="报表导出方式">
        <ExportActionPanel description="立即按当前条件生成并下载，适合小范围数据查阅。" title="直接导出">
          <PermissionButton code="project-management:report:export" disabled={isDirectExportPending} onClick={() => csvMutation.mutate()}>{csvMutation.isPending ? 'CSV 导出中…' : '导出 CSV'}</PermissionButton>
          <PermissionButton code="project-management:report:export" disabled={isDirectExportPending} onClick={() => excelMutation.mutate()}>{excelMutation.isPending ? 'Excel 导出中…' : '导出 Excel'}</PermissionButton>
        </ExportActionPanel>
        <ExportActionPanel description="异步生成并保存结果，适合完整报表和耗时内容。" title="后台快照">
          <PermissionButton code="project-management:report:export" disabled={!canStartSnapshot} onClick={() => snapshotMutation.mutate('pdf')}>生成 PDF 快照</PermissionButton>
          <PermissionButton code="project-management:report:export" disabled={!canStartSnapshot} onClick={() => snapshotMutation.mutate('xlsx')}>生成完整 Excel</PermissionButton>
          <PermissionButton code="project-management:report:export" disabled={!canStartSnapshot} onClick={() => snapshotMutation.mutate('csv')}>生成 CSV 快照</PermissionButton>
        </ExportActionPanel>
      </section>
      {snapshotOperationId ? <ProjectManagementSnapshotJobStatus completedSnapshot={completedSnapshot} downloading={snapshotDownloadMutation.isPending} operationId={snapshotOperationId} retrying={retryMutation.isPending} onDownload={() => snapshotDownloadMutation.mutate()} onRetry={() => retryMutation.mutate()} onTerminal={setCompletedSnapshot} onTrackingEnded={() => { setSnapshotOperationId(null); setCompletedSnapshot(null); }} /> : null}
    </ResponsivePage>
  );
}

function Checkbox({ checked, label, onChange }: { checked: boolean; label: string; onChange: (checked: boolean) => void }) {
  return <label className="flex items-center gap-2"><input checked={checked} type="checkbox" onChange={(event) => onChange(event.target.checked)} /> {label}</label>;
}

function ExportActionPanel({ children, description, title }: { children: React.ReactNode; description: string; title: string }) {
  return <section className="rounded-lg border border-gray-200 bg-white p-4"><h2 className="font-semibold">{title}</h2><p className="mt-1 text-sm text-gray-500">{description}</p><div className="mt-4 flex flex-wrap gap-2">{children}</div></section>;
}

function OptionGroup({ children, title }: { children: React.ReactNode; title: string }) {
  return <fieldset className="rounded border border-gray-200 p-3"><legend className="px-1 text-sm font-medium">{title}</legend><div className="mt-1 flex flex-wrap gap-x-4 gap-y-2 text-sm">{children}</div></fieldset>;
}
