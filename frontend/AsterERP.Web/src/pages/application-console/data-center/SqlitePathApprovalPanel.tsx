import { useMemo, useState } from 'react';

import {
  approveApplicationDataSourceSqlitePathApproval,
  listApplicationDataSourceSqlitePathApprovals,
  rejectApplicationDataSourceSqlitePathApproval,
  requestApplicationDataSourceSqlitePathApproval,
  revokeApplicationDataSourceSqlitePathApproval
} from '../../../api/application-data-center/applicationDataCenter.api';
import type {
  ApplicationDataSourceSqlitePathApproval,
  ApplicationDataSourceSqlitePathApprovalRequest
} from '../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { useMessage } from '../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../shared/utils/errorMessage';

interface SqlitePathApprovalPanelProps {
  dataSourceId: string;
  editPermission: string;
  publishPermission: string;
}

export function SqlitePathApprovalPanel({ dataSourceId, editPermission, publishPermission }: SqlitePathApprovalPanelProps) {
  const message = useMessage();
  const [path, setPath] = useState('');
  const [reason, setReason] = useState('');
  const [expiresAt, setExpiresAt] = useState(() => toDateTimeLocal(new Date(Date.now() + 24 * 60 * 60 * 1000)));
  const approvalsQuery = useApiQuery({
    queryFn: ({ signal }) => listApplicationDataSourceSqlitePathApprovals(dataSourceId, signal),
    queryKey: ['application-data-center', 'sqlite-path-approvals', dataSourceId]
  });

  const requestMutation = useApiMutation({
    mutationFn: (request: ApplicationDataSourceSqlitePathApprovalRequest) => requestApplicationDataSourceSqlitePathApproval(dataSourceId, request),
    onError: (error) => message.error(getErrorMessage(error, translateCurrentLiteral('SQLite 路径申请失败'))),
    onSuccess: async () => {
      message.success(translateCurrentLiteral('SQLite 路径申请已提交'));
      setPath('');
      setReason('');
      await approvalsQuery.refetch();
    }
  });

  const submit = () => {
    const normalizedPath = path.trim();
    const normalizedReason = reason.trim();
    if (!isAbsolutePath(normalizedPath)) {
      message.error(translateCurrentLiteral('例外审批必须填写绝对路径；应用内数据库请使用 sandbox 相对路径。'));
      return;
    }
    if (normalizedReason.length < 10) {
      message.error(translateCurrentLiteral('审批原因至少需要 10 个字符。'));
      return;
    }
    requestMutation.mutate({ dataSourceId, expiresAt: new Date(expiresAt).toISOString(), path: normalizedPath, reason: normalizedReason });
  };

  return (
    <section className="rounded border border-amber-200 bg-amber-50 p-3">
      <div className="text-sm font-semibold text-amber-900">{translateCurrentLiteral('SQLite 外部路径审批')}</div>
      <div className="mt-1 text-xs leading-5 text-amber-800">{translateCurrentLiteral('默认连接只能使用当前租户应用 sandbox 相对路径。绝对路径必须经过审批、期限和审计。')}</div>
      <div className="mt-3 grid gap-2 md:grid-cols-2">
        <input className="h-9 rounded border border-amber-300 bg-white px-2 text-sm" placeholder={translateCurrentLiteral('绝对路径')} value={path} onChange={(event) => setPath(event.target.value)} />
        <input className="h-9 rounded border border-amber-300 bg-white px-2 text-sm" type="datetime-local" value={expiresAt} onChange={(event) => setExpiresAt(event.target.value)} />
        <textarea className="min-h-20 rounded border border-amber-300 bg-white px-2 py-1 text-sm md:col-span-2" placeholder={translateCurrentLiteral('申请原因（至少 10 个字符）')} value={reason} onChange={(event) => setReason(event.target.value)} />
      </div>
      <PermissionButton className="mt-2 primary-button" code={editPermission} disabled={requestMutation.isPending} type="button" onClick={submit}>
        {requestMutation.isPending ? translateCurrentLiteral('提交中…') : translateCurrentLiteral('提交路径申请')}
      </PermissionButton>
      <div className="mt-3 space-y-2">
        {(approvalsQuery.data?.data ?? []).map((approval) => (
          <ApprovalRow key={approval.id} approval={approval} dataSourceId={dataSourceId} publishPermission={publishPermission} onRefresh={approvalsQuery.refetch} />
        ))}
        {!approvalsQuery.isFetching && (approvalsQuery.data?.data ?? []).length === 0 ? <div className="text-xs text-amber-800">{translateCurrentLiteral('暂无路径审批记录。')}</div> : null}
      </div>
    </section>
  );
}

function ApprovalRow({ approval, dataSourceId, publishPermission, onRefresh }: { approval: ApplicationDataSourceSqlitePathApproval; dataSourceId: string; publishPermission: string; onRefresh: () => Promise<unknown> }) {
  const message = useMessage();
  const mutation = useApiMutation({
    mutationFn: (action: 'approve' | 'reject' | 'revoke') => action === 'approve'
      ? approveApplicationDataSourceSqlitePathApproval(dataSourceId, approval.id)
      : action === 'reject'
        ? rejectApplicationDataSourceSqlitePathApproval(dataSourceId, approval.id)
        : revokeApplicationDataSourceSqlitePathApproval(dataSourceId, approval.id),
    onError: (error) => message.error(getErrorMessage(error, translateCurrentLiteral('SQLite 审批操作失败'))),
    onSuccess: async () => {
      message.success(translateCurrentLiteral('SQLite 审批状态已更新'));
      await onRefresh();
    }
  });
  const canApprove = approval.status === 'Pending';
  const canRevoke = approval.status === 'Approved';
  const statusClass = useMemo(() => approval.status === 'Approved' ? 'text-emerald-700' : approval.status === 'Pending' ? 'text-amber-700' : 'text-slate-600', [approval.status]);

  return (
    <div className="rounded border border-amber-200 bg-white p-2 text-xs">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="break-all font-medium text-slate-800">{approval.path}</span>
        <span className={`font-semibold ${statusClass}`}>{approval.status}</span>
      </div>
      <div className="mt-1 text-slate-600">{approval.reason}</div>
      <div className="mt-1 text-slate-500">{translateCurrentLiteral('申请人：')}{approval.requestedBy} {translateCurrentLiteral('· 到期：')}{new Date(approval.expiresAt).toLocaleString()}</div>
      <div className="mt-2 flex flex-wrap gap-2">
        {canApprove ? <PermissionButton className="ghost-button" code={publishPermission} disabled={mutation.isPending} type="button" onClick={() => mutation.mutate('approve')}>{translateCurrentLiteral('批准')}</PermissionButton> : null}
        {canApprove ? <PermissionButton className="ghost-button" code={publishPermission} disabled={mutation.isPending} type="button" onClick={() => mutation.mutate('reject')}>{translateCurrentLiteral('拒绝')}</PermissionButton> : null}
        {canRevoke ? <PermissionButton className="ghost-button" code={publishPermission} disabled={mutation.isPending} type="button" onClick={() => mutation.mutate('revoke')}>{translateCurrentLiteral('撤销')}</PermissionButton> : null}
      </div>
    </div>
  );
}

function isAbsolutePath(value: string): boolean {
  return value.startsWith('/') || /^[A-Za-z]:[\\/]/.test(value) || value.startsWith('\\\\');
}

function toDateTimeLocal(value: Date): string {
  const offset = value.getTimezoneOffset();
  return new Date(value.getTime() - offset * 60_000).toISOString().slice(0, 16);
}
