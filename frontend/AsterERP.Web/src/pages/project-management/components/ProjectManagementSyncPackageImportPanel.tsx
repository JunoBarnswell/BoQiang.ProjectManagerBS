import { useRef, useState } from 'react';

import {
  applyProjectManagementSync,
  previewProjectManagementSync,
  retryProjectManagementSync,
} from '../../../api/project-management/projectManagement.api';
import type {
  ProjectManagementSyncImportResponse,
  ProjectManagementSyncPreviewResponse,
} from '../../../api/project-management/projectManagement.types';
import { usePermission } from '../../../core/auth/usePermission';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { useMessage } from '../../../shared/feedback/useMessage';
import { formatFileSize } from '../../../shared/file-preview/filePreviewUtils';
import { getErrorMessage } from '../../../shared/utils/errorMessage';

type ConflictStrategy = 'Skip' | 'Overwrite' | 'Merge' | 'Reject';
type FailedAction = 'preview' | 'apply' | null;

interface ProjectManagementSyncPackageImportPanelProps {
  deviceId?: string;
  retryHistoryId?: string | null;
  onImportFinished?: () => void;
}

export function ProjectManagementSyncPackageImportPanel({ deviceId, retryHistoryId, onImportFinished }: ProjectManagementSyncPackageImportPanelProps) {
  const { hasPermission: canImport } = usePermission("project-management:sync:import");
  const message = useMessage();
  const [packageFile, setPackageFile] = useState<File | null>(null);
  const [password, setPassword] = useState('');
  const [confirmRisk, setConfirmRisk] = useState(false);
  const [conflictStrategy, setConflictStrategy] = useState<ConflictStrategy>('Skip');
  const [idempotencyKey, setIdempotencyKey] = useState('');
  const [preview, setPreview] = useState<ProjectManagementSyncPreviewResponse | null>(null);
  const [importResult, setImportResult] = useState<ProjectManagementSyncImportResponse | null>(null);
  const [failureMessage, setFailureMessage] = useState<string | null>(null);
  const [failedAction, setFailedAction] = useState<FailedAction>(null);
  const previewAbortController = useRef<AbortController | null>(null);

  const previewMutation = useApiMutation({
    mutationFn: () => {
      if (!packageFile) throw new Error('请先选择同步包');
      previewAbortController.current = new AbortController();
      return previewProjectManagementSync(packageFile, previewAbortController.current.signal);
    },
    onError: (error) => {
      if (error instanceof DOMException && error.name === 'AbortError') return;
      const messageText = getErrorMessage(error, '同步包预览失败');
      setFailureMessage(messageText);
      setFailedAction('preview');
      message.error(messageText);
    },
    onSuccess: (result) => {
      setPreview(result.data);
      setIdempotencyKey(result.data?.packageId ?? '');
      setImportResult(null);
      setFailureMessage(null);
      setFailedAction(null);
      previewAbortController.current = null;
      message.success(result.data?.isCompatible ? '同步包校验通过，请查看预览结果' : '同步包不兼容，无法导入');
    },
  });
  const applyMutation = useApiMutation({
    mutationFn: () => {
      if (!packageFile) throw new Error('请先选择同步包');
      const request = { currentPassword: password, confirmRisk, conflictStrategy, idempotencyKey, deviceId };
      return retryHistoryId
        ? retryProjectManagementSync(retryHistoryId, packageFile, request)
        : applyProjectManagementSync(packageFile, request);
    },
    onError: (error) => {
      const messageText = getErrorMessage(error, '同步包导入失败');
      setFailureMessage(messageText);
      setFailedAction('apply');
      message.error(messageText);
    },
    onSuccess: (result) => {
      const imported = result.data;
      message.success(
        `同步包已导入：新增 ${imported?.inserted ?? 0}，更新 ${imported?.updated ?? 0}，跳过 ${imported?.skipped ?? 0}`,
      );
      setImportResult(imported);
      setPreview(null);
      setPassword('');
      setFailureMessage(null);
      setFailedAction(null);
      onImportFinished?.();
    },
  });

  const startPreview = () => {
    setFailureMessage(null);
    setFailedAction(null);
    previewMutation.mutate();
  };

  const cancelPreview = () => {
    previewAbortController.current?.abort();
    previewAbortController.current = null;
    setFailureMessage(null);
    setFailedAction(null);
  };

  const startImport = () => {
    setFailureMessage(null);
    setFailedAction(null);
    applyMutation.mutate();
  };

  if (!canImport) return null;

  return (
    <div className="mt-3 space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <input
          aria-label="选择同步包"
          type="file"
          accept=".bqsync,application/zip"
          onChange={(event) => {
            setPackageFile(event.target.files?.[0] ?? null);
            setPreview(null);
            setImportResult(null);
            setIdempotencyKey('');
            setFailureMessage(null);
            setFailedAction(null);
          }}
        />
        <PermissionButton
          code="project-management:sync:import"
          disabled={!packageFile || previewMutation.isPending || applyMutation.isPending}
          onClick={startPreview}
        >
          {previewMutation.isPending ? '正在校验同步包…' : '预览同步包'}
        </PermissionButton>
        {previewMutation.isPending ? <button className="rounded border border-gray-300 px-3 py-2" type="button" onClick={cancelPreview}>取消校验</button> : null}
      </div>
      {packageFile ? <p className="text-sm text-gray-500">已选择：{packageFile.name}（{formatFileSize(packageFile.size)}）。服务端会校验包格式、工作区、校验和与冲突。</p> : null}
      {retryHistoryId ? <p className="rounded border border-blue-200 bg-blue-50 p-2 text-sm text-blue-900">正在重试失败批次 {retryHistoryId}：必须重新选择包，服务端会先比对 Package ID，再沿用原有幂等语义执行。</p> : null}
      {previewMutation.isPending ? <div className="rounded border border-blue-200 bg-blue-50 p-3 text-sm text-blue-900" role="status">正在读取同步包并计算影响范围，请勿关闭页面。</div> : null}
      {preview ? (
        <div className="rounded bg-slate-50 p-3 text-sm">
          <div className="font-medium">
            预览结果 · {preview.isCompatible ? "可导入" : "不可导入"} · {preview.packageId}
          </div>
          <div className="mt-2 grid gap-2 sm:grid-cols-3">
            <span>项目 {preview.projectCount}</span>
            <span>任务 {preview.taskCount}</span>
            <span>附件 {preview.attachmentCount}</span>
            <span>成员 {preview.memberCount}</span>
            <span>里程碑 {preview.milestoneCount}</span>
            <span>冲突 {preview.conflicts.length}</span>
            <span>Journal {preview.journalCount}</span>
            <span>签名 {preview.signatureValid ? '有效' : '无效'}</span>
            <span>解压 {formatFileSize(preview.uncompressedSize)}</span>
            <span>模式 {preview.mode === 'Incremental' ? `增量（>${preview.sinceSequenceNo}）` : '全量'}</span>
          </div>
          {preview.conflicts.length ? (
            <div className="mt-2 text-amber-700">冲突：{preview.conflicts.join("；")}</div>
          ) : null}
          {preview.conflictDetails?.length ? (
            <div className="mt-2 space-y-2" aria-label="冲突详情">
              {preview.conflictDetails.map((conflict) => (
                <div className="rounded border border-amber-200 bg-amber-50 p-2 text-amber-900" key={`${conflict.aggregateType}-${conflict.aggregateId}`}>
                  <div>{conflict.aggregateType} / {conflict.aggregateId} · 建议 {conflict.recommendedStrategy}</div>
                  <div className="mt-1 break-all text-xs">本地：{conflict.localValue ?? '无'}</div>
                  <div className="break-all text-xs">远端：{conflict.remoteValue ?? '无'}</div>
                </div>
              ))}
            </div>
          ) : null}
          {preview.alreadyImported ? <div className="mt-2 text-blue-700">该同步包已有成功导入记录；使用相同幂等键执行将直接返回原结果。</div> : null}
          {preview.validationState !== 'Valid' ? <div className="mt-2 text-red-700">验证状态：{preview.validationState}，当前不可导入。</div> : null}
          {preview.warnings.length ? (
            <div className="mt-2 text-amber-700">提示：{preview.warnings.join('；')}</div>
          ) : null}
          <div className="mt-3 rounded border border-amber-200 bg-amber-50 p-3 text-amber-900">
            <p className="font-medium">高风险导入影响与回滚</p>
            <p className="mt-1">将按所选策略写入上述项目、任务及关联数据。服务端使用事务提交；失败时会回滚数据库写入，并尝试补偿本次已写入的附件。</p>
          </div>
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <select
              aria-label="冲突处理策略"
              value={conflictStrategy}
              onChange={(event) => setConflictStrategy(event.target.value as ConflictStrategy)}
            >
              <option value="Skip">跳过冲突</option>
              <option value="Overwrite">覆盖冲突</option>
              <option value="Merge">合并可安全字段</option>
              <option value="Reject">遇到冲突即拒绝</option>
            </select>
            <input
              className="rounded border border-gray-300 px-3 py-2"
              type="password"
              aria-label="导入当前密码"
              placeholder="导入当前密码"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
            <input
              className="rounded border border-gray-300 px-3 py-2"
              aria-label="同步导入幂等键"
              placeholder="幂等键（默认包 ID）"
              value={idempotencyKey}
              onChange={(event) => setIdempotencyKey(event.target.value)}
            />
            <label className="flex items-center gap-2">
              <input type="checkbox" checked={confirmRisk} onChange={(event) => setConfirmRisk(event.target.checked)} />
              确认执行高风险导入
            </label>
            <PermissionButton
              code="project-management:sync:import"
              disabled={!preview.isCompatible || !password || !confirmRisk || applyMutation.isPending}
              onClick={startImport}
            >
              {applyMutation.isPending ? '正在导入…' : '执行导入'}
            </PermissionButton>
          </div>
          {applyMutation.isPending ? <p className="mt-3 text-sm text-blue-800" role="status">导入已提交到服务端，正在执行事务写入和附件校验。完成后会显示结果报告。</p> : null}
        </div>
      ) : null}
      {importResult ? (
        <section className={`rounded border p-3 text-sm ${importResult.warnings.length || importResult.skipped ? 'border-amber-300 bg-amber-50 text-amber-900' : 'border-emerald-300 bg-emerald-50 text-emerald-900'}`} aria-live="polite">
          <h3 className="font-medium">{importResult.warnings.length || importResult.skipped ? '导入完成，但有需要关注的结果' : '导入完成'}</h3>
          <div className="mt-1">
            包 {importResult.packageId} · 策略 {importResult.strategy} · 新增 {importResult.inserted} · 更新{' '}
            {importResult.updated} · 跳过 {importResult.skipped} · 已导入附件 {importResult.attachmentsImported} · 冲突 {importResult.conflictCount}
          </div>
          <div className="mt-1 text-xs">{importResult.replayed ? '本次为幂等重放' : '本次为新导入'} · ImportId {importResult.importId} · TraceId {importResult.traceId}</div>
          {importResult.warnings.length ? <ul className="mt-2 list-disc space-y-1 pl-5">{importResult.warnings.map((warning) => <li key={warning}>{warning}</li>)}</ul> : null}
          {importResult.skipped ? <p className="mt-2">部分记录已按策略跳过；如需重新处理，请重新预览该包并选择适合的冲突策略。</p> : null}
        </section>
      ) : null}
      {failureMessage ? <section className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-900" role="alert"><p>{failedAction === 'apply' ? '导入失败' : '预览失败'}：{failureMessage}</p><button className="mt-2" disabled={!packageFile || previewMutation.isPending || applyMutation.isPending} type="button" onClick={failedAction === 'apply' ? startImport : startPreview}>{failedAction === 'apply' ? '重试导入' : '重试预览'}</button></section> : null}
    </div>
  );
}
