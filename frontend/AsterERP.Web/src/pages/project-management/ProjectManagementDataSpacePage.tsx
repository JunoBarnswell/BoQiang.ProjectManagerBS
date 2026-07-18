import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router-dom';

import {
  createProjectManagementBackup,
  exportProjectManagementSync,
  getProjectManagementBackups,
  getProjectManagementDataSpaceSummary,
  previewProjectManagementBackupRestore,
  restoreProjectManagementBackup
} from '../../api/project-management/projectManagement.api';
import { usePermission } from '../../core/auth/usePermission';
import { isHttpError } from '../../core/http/httpError';
import { queryKeys } from '../../core/query/queryKeys';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useProjectManagementWorkspaceScope } from '../../features/project-management/state/projectManagementWorkspaceScope';
import { toProjectManagementPlatformRoute } from '../../features/project-management/state/projectManagementPlatformRoutes';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { PermissionGuard } from '../../shared/auth/PermissionGuard';
import { useMessage } from '../../shared/feedback/useMessage';
import { ResponsivePage } from '../../shared/responsive/ResponsivePage';
import { Page403 } from '../../shared/status/Page403';
import { PageError } from '../../shared/status/PageError';
import { PageLoading } from '../../shared/status/PageLoading';
import { getErrorMessage } from '../../shared/utils/errorMessage';

import { ProjectManagementSyncPackageImportPanel } from './components/ProjectManagementSyncPackageImportPanel';

export function ProjectManagementDataSpacePage() {
  const scope = useProjectManagementWorkspaceScope();
  const message = useMessage();
  const queryClient = useQueryClient();
  const [password, setPassword] = useState('');
  const [confirmRisk, setConfirmRisk] = useState(false);
  const [restorePreview, setRestorePreview] = useState<NonNullable<Awaited<ReturnType<typeof previewProjectManagementBackupRestore>>['data']> | null>(null);
  const { hasPermission: canManageBackup } = usePermission('project-management:backup:manage');
  const isSystemPlatform = scope.appCode === 'SYSTEM';
  const summaryQuery = useQuery({
    enabled: scope.isAvailable,
    queryKey: queryKeys.projectManagement.dataSpaceSummary(scope),
    queryFn: ({ signal }) => getProjectManagementDataSpaceSummary(signal),
  });
  const backupsQuery = useQuery({
    queryKey: queryKeys.projectManagement.backups(scope),
    queryFn: () => getProjectManagementBackups(),
    enabled: scope.isAvailable && canManageBackup && !isSystemPlatform,
  });
  const backupMutation = useApiMutation({
    mutationFn: () => createProjectManagementBackup({ currentPassword: password, confirmRisk, reason: '项目管理数据空间手动备份' }),
    onError: (error) => message.error(getErrorMessage(error, '备份失败')),
    onSuccess: () => {
      message.success('备份已完成');
      void queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.backups(scope) });
    }
  });
  const restoreMutation = useApiMutation({
    mutationFn: (id: string) => restoreProjectManagementBackup(id, { currentPassword: password, confirmRisk }),
    onError: (error) => message.error(getErrorMessage(error, '恢复失败')),
    onSuccess: async () => {
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

  return (
    <ResponsivePage
      title="项目数据空间"
      eyebrow="ProjectManagement / Data Space"
      description="查看当前授权项目域数据摘要，并通过统一 bqsync 协议进行预览和导出。"
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
        <h2 className="font-semibold">同步包</h2>
        <p className="mt-1 text-sm text-gray-500">导出只包含当前用户有权访问的项目数据；导入前必须经过包校验、冲突预览和高风险确认。</p>
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <PermissionButton code="project-management:sync:export" disabled={exportMutation.isPending} onClick={() => exportMutation.mutate()}>{exportMutation.isPending ? '导出中…' : '导出 bqsync'}</PermissionButton>
        </div>
        <ProjectManagementSyncPackageImportPanel />
      </section>
      <section className="mt-4 rounded-lg border border-gray-200 p-4">
        <h2 className="font-semibold">备份与恢复</h2>
        {isSystemPlatform ? <p className="mt-1 text-sm text-gray-500">平台项目管理暂不支持物理备份/恢复，待 pm_* 逻辑备份能力。</p> : <>
        <p className="mt-1 text-sm text-gray-500">仅支持当前 SQLite 数据空间。恢复会覆盖整个当前数据空间，不只是项目管理记录。</p>
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <input className="rounded border border-gray-300 px-3 py-2" type="password" aria-label="当前密码" placeholder="当前密码" value={password} onChange={(event) => setPassword(event.target.value)} />
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={confirmRisk} onChange={(event) => setConfirmRisk(event.target.checked)} />我确认这是高风险数据操作</label>
          <PermissionButton code="project-management:backup:manage" disabled={!password || !confirmRisk || backupMutation.isPending} onClick={() => backupMutation.mutate()}>创建备份</PermissionButton>
        </div>
        {restorePreview ? <div className="mt-3 rounded border border-amber-300 bg-amber-50 p-3 text-sm"><div className="font-medium">恢复影响确认 · {restorePreview.backup.backupName}</div><p className="mt-1">{restorePreview.impactScope}</p><p className="mt-1">当前记录：项目 {restorePreview.currentDataSpace.projectCount}、任务 {restorePreview.currentDataSpace.taskCount}；备份记录：项目 {restorePreview.backupDataSpace.projectCount}、任务 {restorePreview.backupDataSpace.taskCount}。</p><p className="mt-1">{restorePreview.failureCompensationHint}</p><p className="mt-1">{restorePreview.successfulRestoreRollbackHint}</p><div className="mt-2 flex gap-2"><PermissionButton code="project-management:backup:manage" disabled={!password || !confirmRisk || restoreMutation.isPending} onClick={() => restoreMutation.mutate(restorePreview.backup.id)}>{restoreMutation.isPending ? '恢复中…' : '确认恢复'}</PermissionButton><button type="button" onClick={() => setRestorePreview(null)}>取消</button></div></div> : null}
        {backupsQuery.isLoading ? <div className="mt-3 text-sm text-gray-500">备份列表加载中…</div> : backupsQuery.isError ? <div className="mt-3 rounded bg-amber-50 p-3 text-sm text-amber-800"><div>备份列表加载失败。</div><button type="button" className="mt-2 underline" onClick={() => void backupsQuery.refetch()}>重试</button></div> : backupsQuery.data?.data?.length ? <div className="mt-3 space-y-2">{backupsQuery.data.data.map((backup) => <div className="flex flex-wrap items-center justify-between gap-2 rounded border border-gray-100 p-3 text-sm" key={backup.id}><span>{backup.backupName} · {Math.round(backup.fileSize / 1024)} KB · {backup.sha256.slice(0, 12)}…</span><PermissionButton code="project-management:backup:manage" disabled={restorePreviewMutation.isPending} onClick={() => restorePreviewMutation.mutate(backup.id)}>{restorePreviewMutation.isPending ? '预览中…' : '查看恢复影响'}</PermissionButton></div>)}</div> : <div className="mt-3 text-sm text-gray-500">暂无备份</div>}
        </>}
      </section>
    </ResponsivePage>
  );
}
