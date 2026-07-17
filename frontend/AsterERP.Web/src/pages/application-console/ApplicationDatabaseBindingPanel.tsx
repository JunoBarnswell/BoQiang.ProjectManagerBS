import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';


import {
  saveApplicationDatabaseBinding,
  testApplicationDatabaseBinding
} from '../../api/application-console/applicationConsole.api';
import type {
  ApplicationConsoleSummaryDto,
  ApplicationDatabaseBindingRequest,
  ApplicationDatabaseBindingResponseDto,
  ApplicationDatabaseProvider
} from '../../api/application-console/applicationConsole.types';
import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { translateCurrentLiteral } from '../../core/i18n/I18nProvider';
import { queryKeys } from '../../core/query/queryKeys';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useMessage } from '../../shared/feedback/useMessage';
import { AppIcon } from '../../shared/icons/AppIcon';
import { formatHttpErrorMessage } from '../../shared/utils/formatHttpError';

interface ApplicationDatabaseBindingPanelProps {
  onReload: () => Promise<unknown> | void;
  onSaveBinding?: (request: ApplicationDatabaseBindingRequest) => Promise<ApiEnvelope<ApplicationDatabaseBindingResponseDto>>;
  onTestBinding?: (request: ApplicationDatabaseBindingRequest) => Promise<ApiEnvelope<ApplicationDatabaseBindingResponseDto>>;
  summary: ApplicationConsoleSummaryDto;
}

const providerOptions: Array<{ label: string; value: ApplicationDatabaseProvider }> = [
  { label: 'SQLite', value: 'Sqlite' },
  { label: 'MySQL', value: 'MySql' },
  { label: 'PostgreSQL', value: 'PostgreSQL' },
  { label: 'SQL Server', value: 'SqlServer' }
];

export function ApplicationDatabaseBindingPanel({ onReload, onSaveBinding, onTestBinding, summary }: ApplicationDatabaseBindingPanelProps) {
  const binding = summary.databaseBinding;
  const message = useMessage();
  const queryClient = useQueryClient();
  const [provider, setProvider] = useState<ApplicationDatabaseProvider>(binding.provider ?? 'Sqlite');
  const [displayName, setDisplayName] = useState(binding.displayName ?? `${summary.application.systemName} 应用库`);
  const [databaseName, setDatabaseName] = useState(binding.databaseName ?? `${summary.application.appCode.toLowerCase()}.db`);
  const [connectionString, setConnectionString] = useState('');
  const isSqlite = provider === 'Sqlite';
  const canSubmit = isSqlite ? Boolean(databaseName.trim()) : Boolean(connectionString.trim());
  const isConnectionFailed = binding.isBound && !binding.isReachable;
  const blockedTitle = isConnectionFailed ? '应用数据库连接失败' : '请先绑定应用数据库';
  const blockedDescription = isConnectionFailed
    ? '应用数据库连接或初始化失败，请检查绑定配置和数据库权限。修正 SQLite 数据库文件名或连接串并保存后，应用级基础数据会重新初始化。'
    : '首次进入应用前需要绑定并初始化应用数据库。绑定后会自动创建菜单、权限、应用管理员、默认部门、默认岗位和任职关系。';

  const testMutation = useApiMutation({
    mutationFn: (request: ApplicationDatabaseBindingRequest) => onTestBinding ? onTestBinding(request) : testApplicationDatabaseBinding(request),
    onError: (error) => message.error(formatHttpErrorMessage(error, '应用数据库连接测试失败')),
    onSuccess: () => message.success('应用数据库连接测试成功')
  });
  const saveMutation = useApiMutation({
    mutationFn: (request: ApplicationDatabaseBindingRequest) => onSaveBinding ? onSaveBinding(request) : saveApplicationDatabaseBinding(request),
    onError: (error) => message.error(formatHttpErrorMessage(error, '应用数据库绑定保存失败')),
    onSuccess: async () => {
      message.success('应用数据库绑定已保存');
      setConnectionString('');
      await queryClient.invalidateQueries({
        queryKey: queryKeys.applicationConsole.summary(summary.application.tenantId, summary.application.appCode)
      });
      await queryClient.invalidateQueries({
        queryKey: queryKeys.applicationConsole.databaseBindingStatus(summary.application.tenantId, summary.application.appCode)
      });
      await onReload();
    }
  });

  function buildRequest(): ApplicationDatabaseBindingRequest {
    return {
      connectionString: isSqlite ? null : connectionString,
      databaseName: isSqlite ? databaseName.trim() : null,
      displayName: displayName.trim() || null,
      provider
    };
  }

  async function handleTest() {
    await testMutation.mutateAsync(buildRequest());
  }

  async function handleSave() {
    await saveMutation.mutateAsync(buildRequest());
  }

  return (
    <div className="rounded-md border border-amber-200 bg-white shadow-sm">
      <div className="border-b border-amber-100 bg-amber-50 px-5 py-4">
        <div className="flex items-start gap-3">
          <span className="mt-0.5 flex h-10 w-10 shrink-0 items-center justify-center rounded-md bg-white text-amber-600 shadow-sm">
            <AppIcon className="h-5 w-5" name={isConnectionFailed ? 'warning' : 'database'} />
          </span>
          <div className="min-w-0">
            <div className="text-base font-semibold text-gray-950">{blockedTitle}</div>
            <p className="mt-1 max-w-3xl text-sm leading-6 text-gray-700">{blockedDescription}</p>
            {binding.message ? <p className="mt-1 text-xs text-amber-700">{binding.message}</p> : null}
          </div>
        </div>
      </div>

      <div className="grid gap-5 p-5 lg:grid-cols-[1fr_320px]">
        <div className="space-y-4">
          {!binding.canManage ? (
            <div className="rounded-md border border-gray-200 bg-gray-50 p-4 text-sm leading-6 text-gray-600">{translateCurrentLiteral("当前账号暂无应用数据库绑定权限，请联系平台管理员或租户管理员完成绑定。")}</div>
          ) : (
            <form className="grid gap-4" onSubmit={(event) => event.preventDefault()}>
              <label className="grid gap-1.5 text-sm font-medium text-gray-700">{translateCurrentLiteral("数据库类型")}<select
                  value={provider}
                  onChange={(event) => setProvider(event.target.value as ApplicationDatabaseProvider)}
                  className="h-10 rounded-md border border-gray-300 bg-white px-3 text-sm text-gray-900 outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100"
                >
                  {providerOptions.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </label>

              <label className="grid gap-1.5 text-sm font-medium text-gray-700">{translateCurrentLiteral("显示名称")}<input
                  value={displayName}
                  onChange={(event) => setDisplayName(event.target.value)}
                  placeholder={translateCurrentLiteral("例如：客户A WMS 应用库")}
                  className="h-10 rounded-md border border-gray-300 px-3 text-sm text-gray-900 outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100"
                />
              </label>

              {isSqlite ? (
                <label className="grid gap-1.5 text-sm font-medium text-gray-700">{translateCurrentLiteral("SQLite 数据库文件名")}<input
                    value={databaseName}
                    onChange={(event) => setDatabaseName(event.target.value)}
                    placeholder={translateCurrentLiteral("例如：wms.db")}
                    className="h-10 rounded-md border border-gray-300 px-3 text-sm text-gray-900 outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100"
                  />
                </label>
              ) : (
                <label className="grid gap-1.5 text-sm font-medium text-gray-700">{translateCurrentLiteral("连接串")}<textarea
                    value={connectionString}
                    onChange={(event) => setConnectionString(event.target.value)}
                    placeholder={translateCurrentLiteral("请输入数据库连接串")}
                    rows={4}
                    className="resize-y rounded-md border border-gray-300 px-3 py-2 text-sm leading-6 text-gray-900 outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100"
                  />
                </label>
              )}

              <div className="flex flex-wrap items-center gap-2">
                <button
                  type="button"
                  disabled={!canSubmit || testMutation.isPending || saveMutation.isPending}
                  onClick={() => void handleTest()}
                  className="inline-flex h-10 items-center gap-2 rounded-md border border-gray-300 bg-white px-4 text-sm font-medium text-gray-700 hover:border-primary-300 hover:text-primary-600 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  <AppIcon className={testMutation.isPending ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} name="refresh" />{translateCurrentLiteral("测试连接")}</button>
                <button
                  type="button"
                  disabled={!canSubmit || saveMutation.isPending || testMutation.isPending}
                  onClick={() => void handleSave()}
                  className="inline-flex h-10 items-center gap-2 rounded-md bg-primary-600 px-4 text-sm font-medium text-white hover:bg-primary-700 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  <AppIcon className={saveMutation.isPending ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} name="check" />{translateCurrentLiteral("保存并重新加载")}</button>
              </div>
            </form>
          )}
        </div>

        <div className="rounded-md border border-gray-200 bg-gray-50 p-4">
          <div className="text-sm font-semibold text-gray-900">{translateCurrentLiteral("当前绑定状态")}</div>
          <dl className="mt-3 space-y-3 text-sm">
            <BindingStateRow label="绑定状态" value={binding.isBound ? '已绑定' : '未绑定'} />
            <BindingStateRow label="连接状态" value={binding.isReachable ? '可连接' : '不可连接'} />
            <BindingStateRow label="数据库类型" value={binding.provider ?? '-'} />
            <BindingStateRow label="显示名称" value={binding.displayName ?? '-'} />
            <BindingStateRow label="数据库文件" value={binding.databaseName ?? '-'} />
            <BindingStateRow label="更新时间" value={formatDate(binding.updatedAt)} />
          </dl>
        </div>
      </div>
    </div>
  );
}

function BindingStateRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-start justify-between gap-4">
      <dt className="shrink-0 text-gray-500">{label}</dt>
      <dd className="min-w-0 text-right font-medium text-gray-900">{value}</dd>
    </div>
  );
}

function formatDate(value?: string | null) {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}
