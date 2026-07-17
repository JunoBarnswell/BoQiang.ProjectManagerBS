import { useState } from 'react';


import {
  listApplicationSystemAssignments,
  updateApplicationSystemAssignment
} from '../../../api/application-data-center/applicationDataCenter.api';
import type { ApplicationSystemAssignment } from '../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { useMessage } from '../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { ApplicationConsolePageFrame } from '../ApplicationConsolePageFrame';
import { WorkspacePanel } from '../workspace-shell/WorkspacePanel';
import { WorkspaceToolbar } from '../workspace-shell/WorkspaceToolbar';

import { DataCenterStatusBadge } from './DataCenterStatusBadge';
import { DataCenterWorkspaceShell } from './DataCenterWorkspaceShell';
import { WorkbenchDrawer } from './workbench/WorkbenchDrawer';

export function ApplicationSystemAssignmentsPage() {
  const message = useMessage();
  const [editing, setEditing] = useState<ApplicationSystemAssignment | null>(null);
  const [form, setForm] = useState({
    authorizedObjectIds: '',
    noPermissionDisplay: 'Hide',
    runningVersion: ''
  });

  const query = useApiQuery({
    queryFn: ({ signal }) => listApplicationSystemAssignments(signal),
    queryKey: ['application-data-center', 'application-assignments']
  });

  const saveMutation = useApiMutation({
    mutationFn: () =>
      updateApplicationSystemAssignment({
        appCode: editing?.appCode ?? '',
        authorizedObjectIds: form.authorizedObjectIds
          .split(/[\n,]/)
          .map((item) => item.trim())
          .filter(Boolean),
        noPermissionDisplay: form.noPermissionDisplay,
        runningVersion: form.runningVersion
      }),
    onError: (error) => message.error(getErrorMessage(error, '保存应用系统分配失败')),
    onSuccess: async () => {
      message.success('应用系统分配已保存');
      setEditing(null);
      await query.refetch();
    }
  });

  const items = query.data?.data ?? [];

  return (
    <ApplicationConsolePageFrame density="compact" hideDescription pageKey="data-center">
      {() => (
        <DataCenterWorkspaceShell
          toolbar={
            <WorkspaceToolbar
              actions={(
                <PermissionButton className="secondary-button h-8 text-xs" code="app:data-center:data-source:view" type="button" onClick={() => query.refetch()}>{translateCurrentLiteral("重新加载")}</PermissionButton>
              )}
              density="tight"
              description="维护应用系统、运行版本、无权限显示和授权对象，保存后刷新仍回显。"
              title={translateCurrentLiteral("应用系统分配")}
            />
          }
        >
          <WorkspacePanel bodyClassName="p-0" description="系统分配也接入统一数据工作区链路，不再额外挂一套独立页面骨架。" title={translateCurrentLiteral("分配列表")}>
              <div className="overflow-x-auto">
                <table className="min-w-full text-left text-sm">
                  <thead className="bg-slate-50 text-xs text-slate-500">
                    <tr>
                      <th className="border-b border-slate-200 px-4 py-3 font-medium">{translateCurrentLiteral("应用系统")}</th>
                      <th className="border-b border-slate-200 px-4 py-3 font-medium">{translateCurrentLiteral("运行版本")}</th>
                      <th className="border-b border-slate-200 px-4 py-3 font-medium">{translateCurrentLiteral("无权限显示")}</th>
                      <th className="border-b border-slate-200 px-4 py-3 font-medium">{translateCurrentLiteral("授权对象")}</th>
                      <th className="border-b border-slate-200 px-4 py-3 font-medium">{translateCurrentLiteral("状态")}</th>
                      <th className="border-b border-slate-200 px-4 py-3 font-medium">{translateCurrentLiteral("操作")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {items.map((item) => (
                      <tr className="border-b border-slate-100 last:border-b-0" key={item.tenantAppId}>
                        <td className="px-4 py-3">
                          <div className="font-medium text-slate-950">{item.appName}</div>
                          <div className="mt-1 text-xs text-slate-500">{item.appCode}</div>
                        </td>
                        <td className="px-4 py-3 text-slate-700">{item.runningVersion || '-'}</td>
                        <td className="px-4 py-3 text-slate-700">{formatNoPermission(item.noPermissionDisplay)}</td>
                        <td className="max-w-[360px] truncate px-4 py-3 text-slate-700">{item.authorizedObjectIds.join(', ') || '-'}</td>
                        <td className="px-4 py-3">
                          <DataCenterStatusBadge status={item.status} />
                        </td>
                        <td className="px-4 py-3">
                          <PermissionButton className="secondary-button h-8" code="app:data-center:data-source:edit" type="button" onClick={() => openEdit(item)}>{translateCurrentLiteral("编辑")}</PermissionButton>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {items.length === 0 ? <div className="px-4 py-10 text-center text-sm text-slate-500">{query.isFetching ? '加载中...' : '暂无应用系统'}</div> : null}
          </WorkspacePanel>

          <WorkbenchDrawer
              footer={
                <>
                  <button className="secondary-button" type="button" onClick={() => setEditing(null)}>{translateCurrentLiteral("取消")}</button>
                  <button className="primary-button" type="button" onClick={() => saveMutation.mutate()}>{translateCurrentLiteral("保存")}</button>
                </>
              }
              open={Boolean(editing)}
              title={translateCurrentLiteral("应用系统分配")}
              onClose={() => setEditing(null)}
            >
              <div className="space-y-3">
                <ReadonlyField label="应用系统" value={editing ? `${editing.appName} / ${editing.appCode}` : '-'} />
                <label className="block text-sm font-medium text-slate-700">{translateCurrentLiteral("运行版本")}<input className="form-input mt-1 h-9" value={form.runningVersion} onChange={(event) => setForm({ ...form, runningVersion: event.target.value })} />
                </label>
                <label className="block text-sm font-medium text-slate-700">{translateCurrentLiteral("无权限显示")}<select className="form-input mt-1 h-9" value={form.noPermissionDisplay} onChange={(event) => setForm({ ...form, noPermissionDisplay: event.target.value })}>
                    <option value="Hide">{translateCurrentLiteral("隐藏")}</option>
                    <option value="Disabled">{translateCurrentLiteral("禁用")}</option>
                    <option value="Readonly">{translateCurrentLiteral("只读")}</option>
                  </select>
                </label>
                <label className="block text-sm font-medium text-slate-700">{translateCurrentLiteral("授权对象")}<textarea
                    className="form-input mt-1 min-h-32"
                    placeholder={translateCurrentLiteral("每行一个对象 ID 或编码")}
                    value={form.authorizedObjectIds}
                    onChange={(event) => setForm({ ...form, authorizedObjectIds: event.target.value })}
                  />
                </label>
              </div>
          </WorkbenchDrawer>
        </DataCenterWorkspaceShell>
      )}
    </ApplicationConsolePageFrame>
  );

  function openEdit(item: ApplicationSystemAssignment) {
    setEditing(item);
    setForm({
      authorizedObjectIds: item.authorizedObjectIds.join('\n'),
      noPermissionDisplay: item.noPermissionDisplay || 'Hide',
      runningVersion: item.runningVersion ?? ''
    });
  }
}

function ReadonlyField({ label, value }: { label: string; value: string }) {
  return (
    <div className="text-sm">
      <div className="font-medium text-slate-700">{label}</div>
      <div className="mt-1 rounded-md border border-slate-200 bg-slate-50 px-3 py-2 text-slate-700">{value}</div>
    </div>
  );
}

function formatNoPermission(value: string) {
  if (value === 'Disabled') return '禁用';
  if (value === 'Readonly') return '只读';
  return '隐藏';
}
