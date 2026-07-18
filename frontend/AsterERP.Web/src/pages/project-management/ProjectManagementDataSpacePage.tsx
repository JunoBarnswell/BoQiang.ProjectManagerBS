import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';

import {
  createProjectManagementBackup,
  applyProjectManagementSync,
  exportProjectManagementSync,
  getProjectManagementBackups,
  getProjectManagementDataSpaceSummary,
  previewProjectManagementSync,
  restoreProjectManagementBackup
} from '../../api/project-management/projectManagement.api';
import { usePermission } from '../../core/auth/usePermission';
import { isHttpError } from '../../core/http/httpError';
import { queryKeys } from '../../core/query/queryKeys';
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

export function ProjectManagementDataSpacePage() {
  const scope = useProjectManagementWorkspaceScope();
  const message = useMessage();
  const queryClient = useQueryClient();
  const [packageFile, setPackageFile] = useState<File | null>(null);
  const [password, setPassword] = useState('');
  const [confirmRisk, setConfirmRisk] = useState(false);
  const [conflictStrategy, setConflictStrategy] = useState<'Skip' | 'Overwrite' | 'Reject'>('Skip');
  const [preview, setPreview] = useState<NonNullable<Awaited<ReturnType<typeof previewProjectManagementSync>>['data']> | null>(null);
  const { hasPermission: canManageBackup } = usePermission('project-management:backup:manage');
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
    onSuccess: () => {
      message.success('恢复已完成，请刷新页面确认数据');
      void queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.backups(scope) });
    }
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
  const previewMutation = useApiMutation({
    mutationFn: () => {
      if (!packageFile) throw new Error('请先选择同步包');
      return previewProjectManagementSync(packageFile);
    },
    onError: (error) => message.error(getErrorMessage(error, '同步包预览失败')),
    onSuccess: (result) => { setPreview(result.data); message.success(result.data?.isCompatible ? '同步包校验通过，请查看预览结果' : '同步包不兼容，无法导入'); }
  });
  const applyMutation = useApiMutation({
    mutationFn: () => {
      if (!packageFile) throw new Error('请先选择同步包');
      return applyProjectManagementSync(packageFile, { currentPassword: password, confirmRisk, conflictStrategy });
    },
    onError: (error) => message.error(getErrorMessage(error, '同步包导入失败')),
    onSuccess: (result) => { message.success(`同步包已导入：新增 ${result.data?.inserted ?? 0}，更新 ${result.data?.updated ?? 0}，跳过 ${result.data?.skipped ?? 0}`); setPreview(null); setPackageFile(null); }
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
      toolbar={<span className="text-sm text-gray-500">{summary.tenantId} / {summary.appCode} · {summary.databaseStatus}</span>}
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
          <PermissionGuard code="project-management:sync:import" fallback={null}>
            <input aria-label="选择同步包" type="file" accept=".bqsync,application/zip" onChange={(event) => { setPackageFile(event.target.files?.[0] ?? null); setPreview(null); }} />
            <PermissionButton code="project-management:sync:import" disabled={!packageFile || previewMutation.isPending || applyMutation.isPending} onClick={() => previewMutation.mutate()}>{previewMutation.isPending ? '校验中…' : '预览同步包'}</PermissionButton>
          </PermissionGuard>
        </div>
        {preview ? <div className="mt-3 rounded bg-slate-50 p-3 text-sm"><div className="font-medium">预览结果 · {preview.isCompatible ? '可导入' : '不可导入'} · {preview.packageId}</div><div className="mt-2 grid gap-2 sm:grid-cols-3"><span>项目 {preview.projectCount}</span><span>任务 {preview.taskCount}</span><span>附件 {preview.attachmentCount}</span><span>成员 {preview.memberCount}</span><span>里程碑 {preview.milestoneCount}</span><span>冲突 {preview.conflicts.length}</span></div>{preview.conflicts.length ? <div className="mt-2 text-amber-700">冲突：{preview.conflicts.join('；')}</div> : null}{preview.warnings.length ? <div className="mt-2 text-amber-700">提示：{preview.warnings.join('；')}</div> : null}<div className="mt-3 flex flex-wrap items-center gap-2"><select aria-label="冲突处理策略" value={conflictStrategy} onChange={(event) => setConflictStrategy(event.target.value as typeof conflictStrategy)}><option value="Skip">跳过冲突</option><option value="Overwrite">覆盖冲突</option><option value="Reject">遇到冲突即拒绝</option></select><input className="rounded border border-gray-300 px-3 py-2" type="password" aria-label="导入当前密码" placeholder="导入当前密码" value={password} onChange={(event) => setPassword(event.target.value)} /><label className="flex items-center gap-2"><input type="checkbox" checked={confirmRisk} onChange={(event) => setConfirmRisk(event.target.checked)} />确认执行高风险导入</label><PermissionButton code="project-management:sync:import" disabled={!preview.isCompatible || !password || !confirmRisk || applyMutation.isPending} onClick={() => applyMutation.mutate()}>{applyMutation.isPending ? '导入中…' : '执行导入'}</PermissionButton></div></div> : null}
      </section>
      <section className="mt-4 rounded-lg border border-gray-200 p-4">
        <h2 className="font-semibold">备份与恢复</h2>
        <p className="mt-1 text-sm text-gray-500">仅支持当前 SQLite 数据空间。恢复前会自动创建安全备份并执行完整性检查。</p>
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <input className="rounded border border-gray-300 px-3 py-2" type="password" aria-label="当前密码" placeholder="当前密码" value={password} onChange={(event) => setPassword(event.target.value)} />
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={confirmRisk} onChange={(event) => setConfirmRisk(event.target.checked)} />我确认这是高风险数据操作</label>
          <PermissionButton code="project-management:backup:manage" disabled={!password || !confirmRisk || backupMutation.isPending} onClick={() => backupMutation.mutate()}>创建备份</PermissionButton>
        </div>
        {backupsQuery.isLoading ? <div className="mt-3 text-sm text-gray-500">备份列表加载中…</div> : backupsQuery.isError ? <div className="mt-3 rounded bg-amber-50 p-3 text-sm text-amber-800"><div>备份列表加载失败。</div><button type="button" className="mt-2 underline" onClick={() => void backupsQuery.refetch()}>重试</button></div> : backupsQuery.data?.data?.length ? <div className="mt-3 space-y-2">{backupsQuery.data.data.map((backup) => <div className="flex flex-wrap items-center justify-between gap-2 rounded border border-gray-100 p-3 text-sm" key={backup.id}><span>{backup.backupName} · {Math.round(backup.fileSize / 1024)} KB · {backup.sha256.slice(0, 12)}…</span><PermissionButton code="project-management:backup:manage" disabled={!password || !confirmRisk || restoreMutation.isPending} onClick={() => restoreMutation.mutate(backup.id)}>{restoreMutation.isPending ? '恢复中…' : '恢复此备份'}</PermissionButton></div>)}</div> : <div className="mt-3 text-sm text-gray-500">暂无备份</div>}
      </section>
    </ResponsivePage>
  );
}
