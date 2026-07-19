import { useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';

import type { ProjectManagementAuditDetail, ProjectManagementAuditFieldChange } from '../../../api/project-management/projectManagement.types';
import { usePermission } from '../../../core/auth/usePermission';

interface ProjectManagementAuditDetailDrawerProps {
  detail?: ProjectManagementAuditDetail;
  error: boolean;
  loading: boolean;
  onClose: () => void;
  onRetry: () => void;
  open: boolean;
}

export function ProjectManagementAuditDetailDrawer({ detail, error, loading, onClose, onRetry, open }: ProjectManagementAuditDetailDrawerProps) {
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const previousFocusRef = useRef<HTMLElement | null>(null);
  const { hasPermission: canOpenTraceDiagnostics } = usePermission('system:operation-log:query');

  useEffect(() => {
    if (!open) return;
    previousFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    closeButtonRef.current?.focus();
    return () => previousFocusRef.current?.focus();
  }, [open]);

  if (!open) return null;

  return <div className="fixed inset-0 z-50 bg-slate-950/30" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}>
    <aside aria-label="审计详情" aria-modal="true" className="ml-auto h-full w-full max-w-3xl overflow-y-auto bg-white p-5 shadow-2xl" role="dialog">
      <header className="mb-5 flex items-start justify-between gap-4 border-b border-slate-200 pb-4">
        <div><h2 className="text-lg font-semibold">审计详情</h2><p className="mt-1 text-sm text-slate-500">字段值在服务端脱敏；关联事件按同一 Trace 的发生顺序展示。</p></div>
        <button ref={closeButtonRef} aria-label="关闭审计详情" className="rounded border border-slate-300 px-3 py-1" type="button" onClick={onClose}>关闭</button>
      </header>
      {loading ? <p role="status" className="text-sm text-slate-500">正在加载审计上下文…</p> : null}
      {error ? <p role="alert" className="rounded border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800">审计详情加载失败。<button className="ml-2 underline" type="button" onClick={onRetry}>重试</button></p> : null}
      {!loading && !error && detail ? <div className="space-y-6">
        <section aria-label="操作概览"><h3 className="font-medium">操作概览</h3><dl className="mt-2 grid gap-2 text-sm sm:grid-cols-2"><Detail label="时间" value={new Date(detail.audit.createdTime).toLocaleString()} /><Detail label="操作者" value={detail.audit.actorDisplayName ?? '用户已删除或无权查看'} /><Detail label="项目" value={detail.audit.projectDisplayName ?? '项目已删除或无权查看'} /><Detail label="对象" value={detail.audit.aggregateDisplayName ?? '对象已删除或无权查看'} /><Detail label="操作" value={detail.audit.activityType} /><Detail label="来源" value={`${detail.audit.source}${detail.audit.sourceDeviceId ? ` / ${detail.audit.sourceDeviceId}` : ''}`} /><Detail label="结果" value={detail.audit.isSuccess ? '成功' : '失败'} /><Detail label="摘要" value={detail.audit.summary ?? '—'} /><Detail label="TraceId" value={detail.audit.traceId} mono /></dl>
          {detail.entitySnapshot.isDeleted ? <p className="mt-3 rounded border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">关联实体已删除，已保留该操作的审计摘要：{detail.entitySnapshot.summary ?? '无可展示摘要'}</p> : null}
          {detail.failureReason ? <p className="mt-3 rounded border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800">失败原因：{detail.failureReason}</p> : null}
          {detail.traceDiagnosticsRoute && canOpenTraceDiagnostics ? <Link className="mt-3 inline-block text-sm text-blue-700 underline" to={detail.traceDiagnosticsRoute}>在操作日志中诊断此 Trace</Link> : null}
        </section>
        <section aria-label="字段差异"><h3 className="font-medium">字段差异</h3>{detail.fieldChanges.length ? <div className="mt-2 space-y-3">{detail.fieldChanges.map((change) => <FieldChange key={change.field} change={change} />)}</div> : <p className="mt-2 text-sm text-slate-500">该操作没有可展示的字段差异。</p>}</section>
        {detail.batch ? <section aria-label="批量明细"><h3 className="font-medium">批量明细</h3><p className="mt-2 text-sm text-slate-600">操作 {detail.batch.operationId} · 总计 {detail.batch.totalCount}，成功 {detail.batch.successCount}，失败 {detail.batch.failureCount}</p>{detail.batch.details?.length ? <ul className="mt-2 space-y-2 text-sm">{detail.batch.details.map((item, index) => <li className="rounded border border-slate-200 p-2" key={`${item.aggregateType}-${item.aggregateId}-${index}`}><strong>{item.summary || '对象已删除或无权查看'}</strong>{item.fieldChanges?.map((change) => <FieldChange key={change.field} change={change} compact />)}</li>)}</ul> : null}</section> : null}
        <section aria-label="关联标识"><h3 className="font-medium">关联标识</h3>{detail.references.length ? <ul className="mt-2 flex flex-wrap gap-2 text-sm">{detail.references.map((reference) => <li className="rounded bg-slate-100 px-2 py-1" key={`${reference.kind}-${reference.id}`}>{reference.kind}: <code>{reference.id}</code>{reference.displayName ? ` (${reference.displayName})` : ''}</li>)}</ul> : <p className="mt-2 text-sm text-slate-500">没有可关联的同步、导入、备份或工作流标识。</p>}</section>
        <section aria-label="关联事件"><h3 className="font-medium">关联事件</h3>{detail.relatedEvents.length ? <ol className="mt-2 space-y-2 border-l border-slate-200 pl-4 text-sm">{detail.relatedEvents.map((event) => <li key={`${event.kind}-${event.id}`}><p><span className="font-medium">{causalityLabel(event.causality)}</span> · {new Date(event.occurredAt).toLocaleString()} · {event.kind}{event.activityType ? ` / ${event.activityType}` : ''}{event.status ? ` / ${event.status}` : ''}</p><p className="text-slate-600">{event.summary || '对象已删除或无权查看'}</p></li>)}</ol> : <p className="mt-2 text-sm text-slate-500">没有同 Trace 的关联事件。</p>}</section>
      </div> : null}
    </aside>
  </div>;
}

function Detail({ label, mono = false, value }: { label: string; mono?: boolean; value: string }) {
  return <div><dt className="text-slate-500">{label}</dt><dd className={mono ? 'break-all font-mono text-xs' : 'break-words'}>{value}</dd></div>;
}

function FieldChange({ change, compact = false }: { change: ProjectManagementAuditFieldChange; compact?: boolean }) {
  return <div className={compact ? 'mt-2 border-t border-slate-100 pt-2' : 'rounded border border-slate-200 p-3'}><p className="font-medium">{change.displayName ?? change.field}{change.isSensitive ? <span className="ml-2 text-xs text-amber-700">敏感字段已脱敏</span> : null}</p><div className="mt-2 grid gap-2 sm:grid-cols-2"><Value label="变更前" value={change.before} /><Value label="变更后" value={change.after} /></div></div>;
}

function Value({ label, value }: { label: string; value?: string }) {
  return <div><p className="text-xs text-slate-500">{label}</p><pre className="mt-1 max-h-48 overflow-auto whitespace-pre-wrap break-words rounded bg-slate-50 p-2 text-xs">{formatJsonValue(value)}</pre></div>;
}

function formatJsonValue(value?: string): string {
  if (!value) return '—';
  try { return JSON.stringify(JSON.parse(value), null, 2); } catch { return value; }
}

function causalityLabel(value: string): string {
  if (value === 'Current') return '当前操作';
  if (value === 'Preceded') return '前置事件';
  if (value === 'Followed') return '后续事件';
  return value;
}
