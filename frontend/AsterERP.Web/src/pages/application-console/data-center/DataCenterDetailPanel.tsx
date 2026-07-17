import type {
  ApplicationConnectionDiagnostic,
  ApplicationDataCenterActionResult,
  ApplicationDataCenterObjectDetail,
  ApplicationDataCenterPreviewResponse
} from '../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { WorkspacePanel } from '../workspace-shell/WorkspacePanel';

import { TestResultPanel } from './config-forms/TestResultPanel';
import { ConnectionDiagnosticPanel } from './ConnectionDiagnosticPanel';
import type { DataCenterModuleConfig } from './dataCenterModuleConfig';
import { DataCenterStatusBadge } from './DataCenterStatusBadge';
import { SqlitePathApprovalPanel } from './SqlitePathApprovalPanel';

interface DataCenterDetailPanelProps {
  actionResult?: ApplicationDataCenterActionResult | null;
  diagnostic?: ApplicationConnectionDiagnostic | null;
  config: DataCenterModuleConfig;
  detail?: ApplicationDataCenterObjectDetail | null;
  loading?: boolean;
  preview?: ApplicationDataCenterPreviewResponse | null;
  onDelete: (detail: ApplicationDataCenterObjectDetail) => void;
  onDiagnose?: (detail: ApplicationDataCenterObjectDetail) => void;
  onDisable: (detail: ApplicationDataCenterObjectDetail) => void;
  onEdit: (detail: ApplicationDataCenterObjectDetail) => void;
  onEnable: (detail: ApplicationDataCenterObjectDetail) => void;
  onPreview: (detail: ApplicationDataCenterObjectDetail) => void;
  onPublish: (detail: ApplicationDataCenterObjectDetail) => void;
  onTest: (detail: ApplicationDataCenterObjectDetail) => void;
  onEnterWorkspace?: (detail: ApplicationDataCenterObjectDetail) => void;
}

export function DataCenterDetailPanel({
  actionResult,
  config,
  diagnostic,
  detail,
  loading,
  preview,
  onDelete,
  onDiagnose,
  onDisable,
  onEdit,
  onEnable,
  onPreview,
  onPublish,
  onTest,
  onEnterWorkspace
}: DataCenterDetailPanelProps) {
  if (loading) {
    return <WorkspacePanel bodyClassName="p-4 text-sm text-slate-500" title={translateCurrentLiteral("对象详情")}>{translateCurrentLiteral("正在加载详情...")}</WorkspacePanel>;
  }

  if (!detail) {
    return (
      <WorkspacePanel bodyClassName="p-4" title={translateCurrentLiteral("对象详情")}>
        <div className="text-sm leading-6 text-slate-500">{translateCurrentLiteral("选择列表中的对象查看配置和检测结果。")}</div>
      </WorkspacePanel>
    );
  }

  return (
    <div className="space-y-3">
      <WorkspacePanel bodyClassName="p-4">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="truncate text-base font-semibold text-slate-900">{detail.objectName}</div>
            <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-slate-500">
              <span>{detail.objectCode}</span>
              <span>{detail.objectType}</span>
              <span>v{detail.versionNo}</span>
            </div>
          </div>
          <DataCenterStatusBadge status={detail.status} />
        </div>

        <div className="mt-4 grid grid-cols-2 gap-2 text-xs">
          <div className="rounded border border-slate-100 bg-slate-50 p-2">
            <div className="text-slate-500">{translateCurrentLiteral("端点")}</div>
            <div className="mt-1 truncate font-medium text-slate-800" title={detail.endpoint || '-'}>
              {detail.endpoint || '-'}
            </div>
          </div>
          <div className="rounded border border-slate-100 bg-slate-50 p-2">
            <div className="text-slate-500">{translateCurrentLiteral("环境")}</div>
            <div className="mt-1 truncate font-medium text-slate-800" title={detail.environment || '-'}>
              {detail.environment || '-'}
            </div>
          </div>
          <div className="rounded border border-slate-100 bg-slate-50 p-2">
            <div className="text-slate-500">{translateCurrentLiteral("负责人")}</div>
            <div className="mt-1 truncate font-medium text-slate-800" title={detail.ownerName || detail.ownerUserId || '-'}>
              {detail.ownerName || detail.ownerUserId || '-'}
            </div>
          </div>
          <div className="rounded border border-slate-100 bg-slate-50 p-2">
            <div className="text-slate-500">{translateCurrentLiteral("最近检测")}</div>
            <div className="mt-1 truncate font-medium text-slate-800" title={detail.lastValidationStatus || '-'}>
              {detail.lastValidationStatus || '-'}
            </div>
          </div>
          <div className="rounded border border-slate-100 bg-slate-50 p-2">
            <div className="text-slate-500">{translateCurrentLiteral("凭据引用")}</div>
            <div className="mt-1 truncate font-medium text-slate-800" title={detail.secretRef || translateCurrentLiteral("未配置")}>
              {detail.secretRef ? `SecretRef · ${detail.secretRef}` : translateCurrentLiteral("未配置")}
            </div>
          </div>
        </div>

        {detail.lastValidationMessage ? (
          <div className="mt-3 rounded border border-slate-100 bg-slate-50 px-3 py-2 text-xs leading-5 text-slate-600">
            {detail.lastValidationMessage}
          </div>
        ) : null}

        <div className="mt-4 flex flex-wrap gap-2">
          {onDiagnose ? <PermissionButton className="ghost-button" code={config.permissions.test} type="button" onClick={() => onDiagnose(detail)}>{translateCurrentLiteral("诊断")}</PermissionButton> : null}
          <PermissionButton className="ghost-button" code={config.permissions.edit} type="button" onClick={() => onEdit(detail)}>{translateCurrentLiteral("编辑")}</PermissionButton>
          <PermissionButton className="ghost-button" code={config.permissions.test} type="button" onClick={() => onTest(detail)}>{translateCurrentLiteral("测试")}</PermissionButton>
          <PermissionButton className="ghost-button" code={config.permissions.preview} type="button" onClick={() => onPreview(detail)}>{translateCurrentLiteral("预览")}</PermissionButton>
          {onEnterWorkspace ? (
            <PermissionButton className="ghost-button" code={config.permissions.view} type="button" onClick={() => onEnterWorkspace(detail)}>{translateCurrentLiteral("进入数据库工作台")}</PermissionButton>
          ) : null}
          <PermissionButton className="ghost-button" code={config.permissions.publish} type="button" onClick={() => onPublish(detail)}>{translateCurrentLiteral("发布")}</PermissionButton>
          {detail.status === 'Disabled' ? (
            <PermissionButton className="ghost-button" code={config.permissions.enable} type="button" onClick={() => onEnable(detail)}>{translateCurrentLiteral("启用")}</PermissionButton>
          ) : (
            <PermissionButton className="ghost-button" code={config.permissions.disable} type="button" onClick={() => onDisable(detail)}>{translateCurrentLiteral("停用")}</PermissionButton>
          )}
          <PermissionButton className="danger-button" code={config.permissions.delete} type="button" onClick={() => onDelete(detail)}>{translateCurrentLiteral("删除")}</PermissionButton>
        </div>
      </WorkspacePanel>

      {actionResult ? <TestResultPanel result={actionResult} /> : null}
      {diagnostic ? <ConnectionDiagnosticPanel diagnostic={diagnostic} /> : null}

      {detail.objectType === 'Sqlite' || detail.objectType === 'ApplicationDatabase' ? (
        <SqlitePathApprovalPanel
          dataSourceId={detail.id}
          editPermission={config.permissions.edit}
          publishPermission={config.permissions.publish}
        />
      ) : null}

      {preview ? <PreviewPanel preview={preview} /> : null}
    </div>
  );
}

function PreviewPanel({ preview }: { preview: ApplicationDataCenterPreviewResponse }) {
  const columns = preview.fields.length > 0 ? preview.fields.map((field) => field.fieldCode) : Object.keys(preview.rows[0] ?? {});

  return (
    <section className="rounded-md border border-slate-200 bg-white p-3">
      <div className="flex items-center justify-between gap-2">
        <div className="text-sm font-semibold text-slate-900">{translateCurrentLiteral("预览数据")}</div>
        <span className="text-xs text-slate-500">{preview.rows.length} {translateCurrentLiteral("行")}</span>
      </div>
      {preview.message ? <div className="mt-2 text-xs leading-5 text-slate-500">{preview.message}</div> : null}
      <div className="mt-3 max-h-64 overflow-auto rounded border border-slate-100">
        <table className="min-w-full text-xs">
          <thead className="bg-slate-50 text-slate-500">
            <tr>
              {columns.map((column) => (
                <th key={column} className="whitespace-nowrap px-2 py-1.5 text-left font-medium">
                  {column}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {preview.rows.slice(0, 10).map((row, index) => (
              <tr key={index} className="border-t border-slate-100">
                {columns.map((column) => (
                  <td key={column} className="max-w-[160px] truncate px-2 py-1.5 text-slate-700">
                    {String(row[column] ?? '')}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
