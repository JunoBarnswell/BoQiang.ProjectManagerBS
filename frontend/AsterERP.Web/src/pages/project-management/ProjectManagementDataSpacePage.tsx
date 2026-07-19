import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router-dom';

import {
  createProjectManagementBackup,
  downloadProjectManagementDataSpaceExport,
  exportProjectManagementSync,
  getProjectManagementBackups,
  getProjectManagementDataSpaceExports,
  getProjectManagementDataSpaceSummary,
  previewProjectManagementBackupRestore,
  restoreProjectManagementBackup,
  startProjectManagementDataSpaceExport,
  startProjectManagementDataSpaceImport,
} from '../../api/project-management/projectManagement.api';
import { usePermission } from '../../core/auth/usePermission';
import { isHttpError } from '../../core/http/httpError';
import { queryKeys } from '../../core/query/queryKeys';
import { useApiMutation } from '../../core/query/useApiMutation';
import { ProjectManagementOperationProgress } from '../../features/project-management/components/ProjectManagementOperationProgress';
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

import { ProjectManagementExcelImportPanel } from './components/ProjectManagementExcelImportPanel';
import { ProjectManagementSyncPackageImportPanel } from './components/ProjectManagementSyncPackageImportPanel';

export function ProjectManagementDataSpacePage() {
  const scope = useProjectManagementWorkspaceScope();
  const message = useMessage();
  const queryClient = useQueryClient();
  const [password, setPassword] = useState('');
  const [confirmRisk, setConfirmRisk] = useState(false);
  const [operationId, setOperationId] = useState<string | null>(null);
  const [operationResult, setOperationResult] = useState<string | null>(null);
  const [restorePreview, setRestorePreview] = useState<NonNullable<Awaited<ReturnType<typeof previewProjectManagementBackupRestore>>['data']> | null>(null);
  const { hasPermission: canManageBackup } = usePermission('project-management:backup:manage');
  const { hasPermission: canExportDatabase } = usePermission('project-management:data-space:export');
  const { hasPermission: canImportDatabase } = usePermission('project-management:data-space:import');
  const summaryQuery = useQuery({
    enabled: scope.isAvailable,
    queryKey: queryKeys.projectManagement.dataSpaceSummary(scope),
    queryFn: ({ signal }) => getProjectManagementDataSpaceSummary(signal),
  });
  const backupsQuery = useQuery({
    queryKey: queryKeys.projectManagement.backups(scope),
    queryFn: () => getProjectManagementBackups(),
    enabled: scope.isAvailable && canManageBackup,
  });
  const databaseExportsQuery = useQuery({
    queryKey: [...queryKeys.projectManagement.backups(scope), 'database-exports'],
    queryFn: () => getProjectManagementDataSpaceExports(),
    enabled: scope.isAvailable && canExportDatabase,
  });
  const databaseExportMutation = useApiMutation({
    mutationFn: () => startProjectManagementDataSpaceExport({ currentPassword: password, confirmRisk, reason: '项目管理数据空间整库导出' }),
    onError: (error) => message.error(getErrorMessage(error, '整库导出任务创建失败')),
    onSuccess: (result) => {
      const exportTask = result.data;
      setOperationId(exportTask?.operationId ?? null);
      setOperationResult(exportTask ? `整库导出已进入后台队列，下载有效至 ${new Date(exportTask.downloadExpiresAt).toLocaleString()}。` : '整库导出已进入后台队列。');
      void queryClient.invalidateQueries({ queryKey: [...queryKeys.projectManagement.backups(scope), 'database-exports'] });
    },
  });
  const databaseExportDownloadMutation = useApiMutation({
    mutationFn: (id: string) => downloadProjectManagementDataSpaceExport(id),
    onError: (error) => message.error(getErrorMessage(error, '导出包下载失败')),
    onSuccess: ({ blob, fileName }) => {
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = fileName;
      anchor.click();
      URL.revokeObjectURL(url);
      void databaseExportsQuery.refetch();
    },
  });
  const databaseImportMutation = useApiMutation({
    mutationFn: (exportId: string) => startProjectManagementDataSpaceImport({ exportId, currentPassword: password, confirmRisk, reason: '项目管理数据空间受控整库导入' }),
    onError: (error) => message.error(getErrorMessage(error, '整库导入任务创建失败')),
    onSuccess: (result) => {
      const task = result.data;
      setOperationId(task?.operationId ?? null);
      setOperationResult(task ? '整库导入已进入维护队列：系统会校验清单、自动备份并在失败时回滚。' : '整库导入已进入维护队列。');
    },
  });
  const backupMutation = useApiMutation({
    mutationFn: () => createProjectManagementBackup({ currentPassword: password, confirmRisk, reason: '项目管理数据空间手动备份' }),
    onError: (error) => message.error(getErrorMessage(error, '备份失败')),
    onSuccess: (result) => {
      const backup = result.data;
      setOperationId(backup?.operationId ?? null);
      setOperationResult(backup ? `备份“${backup.backupName}”已完成，校验摘要 ${backup.sha256.slice(0, 12)}…` : '备份已完成。');
      message.success('备份已完成，可在下方查看审计状态');
      void queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.backups(scope) });
    }
  });
  const restoreMutation = useApiMutation({
    mutationFn: (id: string) => restoreProjectManagementBackup(id, { currentPassword: password, confirmRisk }),
    onError: (error) => message.error(getErrorMessage(error, '恢复失败')),
    onSuccess: async (result) => {
      const backup = result.data;
      setOperationId(backup?.operationId ?? null);
      setOperationResult(backup ? `已从备份“${backup.backupName}”恢复当前项目管理数据空间。` : '恢复已完成，数据空间已刷新。');
      setRestorePreview(null);
      await queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.all(scope) });
      message.success('恢复已完成，项目管理数据已刷新');
    }
  });
  const restorePreviewMutation = useApiMutation({
    mutationFn: (id: string) => previewProjectManagementBackupRestore(id),
    onError: (error) => message.error(getErrorMessage(error, '恢复影响预览失败')),
    onSuccess: (result) => { if (result.data) setRestorePreview(result.data); }
  });
  const exportMutation = useApiMutation({
    mutationFn: () => exportProjectManagementSync({ includeAttachments: false, deviceId: 'browser' }),
    onError: (error) => message.error(getErrorMessage(error, '同步包导出失败')),
    onSuccess: ({ blob, fileName }) => {
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = fileName;
      anchor.click();
      URL.revokeObjectURL(url);
      message.success('同步包已生成');
    }
  });
  if (summaryQuery.isLoading) return <PageLoading />;
  if (summaryQuery.isError) {
    if (isHttpError(summaryQuery.error) && summaryQuery.error.status === 403) return <Page403 />;
    return <PageError action={<button type="button" onClick={() => void summaryQuery.refetch()}>重试</button>} description="数据空间摘要加载失败" />;
  }
  const summary = summaryQuery.data?.data;
  if (!summary) return <PageError description="数据空间摘要为空" />;
  const restoreWarnings = restorePreview ? buildRestoreWarnings(restorePreview.currentDataSpace, restorePreview.backupDataSpace) : [];

  return (
    <ResponsivePage
      title="项目数据空间"
      eyebrow="ProjectManagement / Data Space"
      description="当前平台工作区内项目管理模块的完整逻辑数据集。备份与恢复不会覆盖平台中的用户、权限、应用或其他模块数据。"
      toolbar={<div className="flex flex-wrap items-center gap-3 text-sm"><span className="text-gray-500">{summary.tenantId} / {summary.appCode} · {summary.databaseStatus}</span><PermissionGuard code="project-management:sync:export" fallback={null}><Link to={toProjectManagementPlatformRoute('project-sync')}>查看同步水位</Link></PermissionGuard><Link to={toProjectManagementPlatformRoute('project-search')}>项目搜索</Link></div>}
    >
      <div className="grid gap-3 md:grid-cols-5">
        {[
          ['项目', summary.projectCount],
          ['任务', summary.taskCount],
          ['成员', summary.memberCount],
          ['里程碑', summary.milestoneCount],
          ['附件', summary.attachmentCount]
        ].map(([label, value]) => <div className="rounded-lg border border-gray-200 p-4" key={label}><div className="text-sm text-gray-500">{label}</div><div className="mt-1 text-2xl font-semibold">{value}</div></div>)}
      </div>
      <section className="mt-4 rounded-lg border border-gray-200 p-4">
        <h2 className="font-semibold">整库导出</h2>
        <p className="mt-1 text-sm text-gray-500">后台使用 SQLite 在线一致性快照，不进入维护模式；生成的 .bqdbx 包已加密，包含架构清单、租户/应用、版本、时间和校验摘要。包默认 24 小时有效，最多下载 3 次。</p>
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <PermissionButton code="project-management:data-space:export" disabled={!password || !confirmRisk || databaseExportMutation.isPending} onClick={() => databaseExportMutation.mutate()}>{databaseExportMutation.isPending ? '正在入队…' : '创建整库导出'}</PermissionButton>
        </div>
        {databaseExportsQuery.isLoading ? <div className="mt-3 text-sm text-gray-500">整库导出记录加载中…</div> : databaseExportsQuery.data?.data?.length ? <div className="mt-3 space-y-2">{databaseExportsQuery.data.data.map((item) => <div className="flex flex-wrap items-center justify-between gap-2 rounded border border-gray-100 p-3 text-sm" key={item.id}><span>{item.packageName} · {item.status} · 下载 {item.downloadCount}/{item.maxDownloadCount} · 有效至 {new Date(item.downloadExpiresAt).toLocaleString()}{item.manifest ? ` · schema v${item.manifest.schemaVersion}` : ''}</span><div className="flex gap-2"><PermissionButton code="project-management:data-space:export" disabled={item.status !== 'Ready' || databaseExportDownloadMutation.isPending} onClick={() => databaseExportDownloadMutation.mutate(item.id)}>{databaseExportDownloadMutation.isPending ? '下载中…' : '下载加密包'}</PermissionButton>{canImportDatabase ? <PermissionButton code="project-management:data-space:import" disabled={item.status !== 'Ready' || !password || !confirmRisk || databaseImportMutation.isPending} onClick={() => databaseImportMutation.mutate(item.id)}>{databaseImportMutation.isPending ? '正在入队…' : '从此包整库导入'}</PermissionButton> : null}</div></div>)}</div> : <div className="mt-3 text-sm text-gray-500">暂无整库导出记录</div>}
      </section>
      <section className="mt-4 rounded-lg border border-gray-200 p-4">
        <h2 className="font-semibold">同步包</h2>
        <p className="mt-1 text-sm text-gray-500">导出只包含当前用户有权访问的项目数据；导入前必须经过包校验、冲突预览和高风险确认。</p>
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <PermissionButton code="project-management:sync:export" disabled={exportMutation.isPending} onClick={() => exportMutation.mutate()}>{exportMutation.isPending ? '导出中…' : '导出 bqsync'}</PermissionButton>
        </div>
        <ProjectManagementSyncPackageImportPanel />
        <ProjectManagementExcelImportPanel />
      </section>
      <section className="mt-4 rounded-lg border border-gray-200 p-4">
        <h2 className="font-semibold">备份与恢复</h2>
        <p className="mt-1 text-sm text-gray-500">备份范围是当前租户与 SYSTEM 工作区的项目、成员、里程碑、任务、附件和关联配置。恢复仅替换这些项目管理记录，不覆盖平台其他模块。</p>
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <input className="rounded border border-gray-300 px-3 py-2" type="password" aria-label="当前密码" placeholder="当前密码" value={password} onChange={(event) => setPassword(event.target.value)} />
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={confirmRisk} onChange={(event) => setConfirmRisk(event.target.checked)} />我确认这是高风险数据操作</label>
          <PermissionButton code="project-management:backup:manage" disabled={!password || !confirmRisk || backupMutation.isPending} onClick={() => backupMutation.mutate()}>创建备份</PermissionButton>
        </div>
        {restorePreview ? <div className="mt-3 rounded border border-amber-300 bg-amber-50 p-3 text-sm"><div className="font-medium">恢复影响确认 · {restorePreview.backup.backupName}</div><p className="mt-1">{restorePreview.impactScope}</p><p className="mt-1">当前记录：项目 {restorePreview.currentDataSpace.projectCount}、任务 {restorePreview.currentDataSpace.taskCount}；备份记录：项目 {restorePreview.backupDataSpace.projectCount}、任务 {restorePreview.backupDataSpace.taskCount}。</p>{restoreWarnings.length ? <div className="mt-2 rounded border border-amber-200 bg-white/70 p-2"><div className="font-medium text-amber-900">部分恢复警告</div><ul className="mt-1 list-disc space-y-1 pl-5">{restoreWarnings.map((warning) => <li key={warning}>{warning}</li>)}</ul></div> : <p className="mt-2 text-emerald-800">计数一致：未发现需要额外确认的对象数量差异。</p>}<p className="mt-2">失败补偿：{restorePreview.failureCompensationHint}</p><p className="mt-1">成功后回滚：{restorePreview.successfulRestoreRollbackHint}</p><div className="mt-2 flex gap-2"><PermissionButton code="project-management:backup:manage" disabled={!password || !confirmRisk || restoreMutation.isPending} onClick={() => restoreMutation.mutate(restorePreview.backup.id)}>{restoreMutation.isPending ? '恢复中…' : '确认恢复'}</PermissionButton><button type="button" onClick={() => setRestorePreview(null)}>取消</button></div></div> : null}
        {backupsQuery.isLoading ? <div className="mt-3 text-sm text-gray-500">备份列表加载中…</div> : backupsQuery.isError ? <div className="mt-3 rounded bg-amber-50 p-3 text-sm text-amber-800"><div>备份列表加载失败。</div><button type="button" className="mt-2 underline" onClick={() => void backupsQuery.refetch()}>重试</button></div> : backupsQuery.data?.data?.length ? <div className="mt-3 space-y-2">{backupsQuery.data.data.map((backup) => <div className="flex flex-wrap items-center justify-between gap-2 rounded border border-gray-100 p-3 text-sm" key={backup.id}><span>{backup.backupName} · {Math.round(backup.fileSize / 1024)} KB · {backup.sha256.slice(0, 12)}…</span><PermissionButton code="project-management:backup:manage" disabled={restorePreviewMutation.isPending} onClick={() => restorePreviewMutation.mutate(backup.id)}>{restorePreviewMutation.isPending ? '预览中…' : '查看恢复影响'}</PermissionButton></div>)}</div> : <div className="mt-3 text-sm text-gray-500">暂无备份</div>}
        {operationResult ? <div className="mt-3 rounded border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-900" role="status">{operationResult}</div> : null}
        {operationId ? <PermissionGuard code="project-management:operation:view" fallback={<p className="mt-3 text-sm text-gray-500">当前账号无权读取任务追踪详情，请到审计中心查看操作记录。</p>}><div className="mt-3"><ProjectManagementOperationProgress operationId={operationId} onTrackingEnded={() => setOperationId(null)} /></div></PermissionGuard> : null}
      </section>
    </ResponsivePage>
  );
}

function buildRestoreWarnings(current: { projectCount: number; taskCount: number; memberCount: number; milestoneCount: number; attachmentCount: number }, backup: { projectCount: number; taskCount: number; memberCount: number; milestoneCount: number; attachmentCount: number }): string[] {
  const labels: Array<[keyof typeof current, string]> = [
    ['projectCount', '项目'],
    ['taskCount', '任务'],
    ['memberCount', '成员'],
    ['milestoneCount', '里程碑'],
    ['attachmentCount', '附件']
  ];
  return labels.flatMap(([key, label]) => current[key] === backup[key] ? [] : [`${label}数量将从 ${current[key]} 变为 ${backup[key]}。`]);
}
