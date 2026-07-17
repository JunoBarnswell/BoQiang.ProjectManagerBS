import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo } from 'react';

import type { TenantAppCatalogItemDto } from '../../api/platform/platform.types';
import { disableTenantApp, enableTenantApp, getTenantAppCatalog, installTenantApp } from '../../api/platform/tenant-management.api';
import { useI18n } from '../../core/i18n/I18nProvider';
import { queryKeys } from '../../core/query/queryKeys';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useMessage } from '../../shared/feedback/useMessage';
import { ResponsivePage } from '../../shared/responsive/ResponsivePage';
import { PageError } from '../../shared/status/PageError';
import { PageLoading } from '../../shared/status/PageLoading';
import { DataTable } from '../../shared/table/DataTable';
import type { DataTableColumn } from '../../shared/table/tableTypes';

export function TenantAppsPage() {
  const { translate } = useI18n();
  const message = useMessage();
  const queryClient = useQueryClient();

  const catalogQuery = useQuery({
    queryFn: getTenantAppCatalog,
    queryKey: queryKeys.tenant.appsCatalog()
  });

  const refreshTenantApps = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.tenant.appsCatalog() }),
      queryClient.invalidateQueries({ queryKey: queryKeys.tenant.appsInstalled() })
    ]);
  };

  const installMutation = useApiMutation({
    mutationFn: (appCode: string) => installTenantApp(appCode, {}),
    onSuccess: async () => {
      message.success(translate('page.platformTenantApps.success.install'));
      await refreshTenantApps();
    }
  });

  const enableMutation = useApiMutation({
    mutationFn: (appCode: string) => enableTenantApp(appCode),
    onSuccess: async () => {
      message.success(translate('page.platformTenantApps.success.enable'));
      await refreshTenantApps();
    }
  });

  const disableMutation = useApiMutation({
    mutationFn: (appCode: string) => disableTenantApp(appCode),
    onSuccess: async () => {
      message.success(translate('page.platformTenantApps.success.disable'));
      await refreshTenantApps();
    }
  });

  const columns: DataTableColumn<TenantAppCatalogItemDto>[] = useMemo(
    () => [
      { key: 'appName', title: translate('page.platformTenantApps.field.appName'), responsivePriority: 100 },
      { key: 'appCode', title: translate('page.platformTenantApps.field.appCode'), width: '110px' },
      { key: 'appType', title: translate('page.platformTenantApps.field.appType'), width: '110px' },
      { key: 'systemName', title: translate('page.platformTenantApps.field.systemName'), width: '180px', render: (row) => row.systemName ?? row.appName },
      { key: 'version', title: translate('page.platformTenantApps.field.version'), width: '100px', render: (row) => row.version ?? '-' },
      {
        align: 'center',
        key: 'tenantAppStatus',
        title: translate('page.platformTenantApps.field.status'),
        width: '100px',
        render: (row) => {
          if (!row.installed) {
            return translate('page.platformTenantApps.status.notInstalled');
          }

          return row.tenantAppStatus === 'Enabled' ? translate('platform.common.enabled') : translate('platform.common.disabled');
        }
      }
    ],
    [translate]
  );

  const rows = catalogQuery.data?.data ?? [];
  const actionPending = installMutation.isPending || enableMutation.isPending || disableMutation.isPending;

  if (catalogQuery.isLoading) {
    return <PageLoading />;
  }

  if (catalogQuery.isError) {
    return <PageError action={<button onClick={() => void catalogQuery.refetch()}>{translate('common.retry')}</button>} description={translate('page.platformTenantApps.error.loadFailed')} />;
  }

  return (
    <ResponsivePage
      description={translate('page.platformTenantApps.description')}
      fitScreen
      title={translate('page.platformTenantApps.title')}
    >
      <DataTable
        columnSettingsKey="tenant-app-catalog"
        columns={columns}
        emptyText={translate('page.platformTenantApps.empty')}
        loading={catalogQuery.isFetching}
        rowActions={(row) => {
          if (!row.installed) {
            return (
              <button disabled={actionPending} onClick={() => installMutation.mutate(row.appCode)}>
                {translate('page.platformTenantApps.action.install')}
              </button>
            );
          }

          if (row.tenantAppStatus === 'Enabled') {
            return (
              <button disabled={actionPending} onClick={() => disableMutation.mutate(row.appCode)}>
                {translate('page.platformTenantApps.action.disable')}
              </button>
            );
          }

          return (
            <button disabled={actionPending} onClick={() => enableMutation.mutate(row.appCode)}>
              {translate('page.platformTenantApps.action.enable')}
            </button>
          );
        }}
        rowKey={(row) => row.appCode}
        rows={rows}
        showColumnSettings
      />
    </ResponsivePage>
  );
}
