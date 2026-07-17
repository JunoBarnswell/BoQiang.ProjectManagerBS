import { useMemo } from 'react';

import { createTenantApp, deleteTenantApp, getTenantApps, updateTenantApp } from '../../api/platform/platform-management.api';
import type { TenantAppListItemDto, TenantAppUpsertRequest } from '../../api/platform/platform.types';
import { useI18n } from '../../core/i18n/I18nProvider';
import { queryKeys } from '../../core/query/queryKeys';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import type { DataTableColumn } from '../../shared/table/tableTypes';

import { PlatformResourcePage } from './PlatformResourcePage';

const defaultFormState: TenantAppUpsertRequest = {
  appCode: '',
  configJson: '',
  expiredAt: null,
  faviconFileId: '',
  logoFileId: '',
  primaryColor: '#1677ff',
  remark: '',
  status: 'Enabled',
  systemName: '',
  tenantId: ''
};

export function PlatformTenantAppsPage() {
  const { translate } = useI18n();

  const statusFilterOptions = useMemo(
    () => [
      { label: translate('platform.common.enabled'), value: 'Enabled' },
      { label: translate('platform.common.disabled'), value: 'Disabled' }
    ],
    [translate]
  );

  const columns = useMemo<DataTableColumn<TenantAppListItemDto>[]>(
    () => [
      { key: 'tenantName', title: translate('page.platformTenantApps.field.tenantName'), responsivePriority: 100, sortable: true, filterable: true, filterType: 'text' },
      { key: 'appName', title: translate('page.platformApplications.field.appName'), responsivePriority: 95, sortable: true, filterable: true, filterType: 'text' },
      { key: 'appCode', title: translate('page.platformTenantApps.field.appCode'), width: '110px', sortable: true, filterable: true, filterType: 'text' },
      { key: 'systemName', title: translate('page.platformTenantApps.field.systemName'), width: '180px', render: (row) => row.systemName ?? '-', sortable: true, filterable: true, filterType: 'text' },
      { key: 'primaryColor', title: translate('page.platformTenantApps.field.primaryColor'), width: '100px', render: (row) => row.primaryColor ?? '-' },
      { key: 'status', title: translate('page.platformTenantApps.field.status'), width: '90px', align: 'center', render: (row) => (row.status === 'Enabled' ? translate('platform.common.enabled') : translate('platform.common.disabled')), sortable: true, filterable: true, filterType: 'select', filterOptions: statusFilterOptions }
    ],
    [statusFilterOptions, translate]
  );

  const fields = useMemo<FormFieldConfig<TenantAppUpsertRequest>[]>(
    () => [
      { label: translate('page.platformTenantApps.field.tenantId'), name: 'tenantId', required: true, span: 1, type: 'text', section: translate('page.platformTenantApps.section.installation') },
      { label: translate('page.platformTenantApps.field.appCode'), name: 'appCode', required: true, span: 1, type: 'text', section: translate('page.platformTenantApps.section.installation') },
      { label: translate('page.platformTenantApps.field.status'), name: 'status', required: true, span: 1, type: 'select', options: [{ label: translate('platform.common.enabled'), value: 'Enabled' }, { label: translate('platform.common.disabled'), value: 'Disabled' }], section: translate('page.platformTenantApps.section.installation') },
      { label: translate('page.platformTenantApps.field.systemName'), name: 'systemName', span: 1, type: 'text', section: translate('page.platformTenantApps.section.branding') },
      { label: translate('page.platformTenantApps.field.primaryColor'), name: 'primaryColor', span: 1, type: 'text', section: translate('page.platformTenantApps.section.branding') },
      { label: translate('page.platformTenantApps.field.logoFileId'), name: 'logoFileId', span: 1, type: 'text', section: translate('page.platformTenantApps.section.branding') },
      { label: translate('page.platformTenantApps.field.faviconFileId'), name: 'faviconFileId', span: 1, type: 'text', section: translate('page.platformTenantApps.section.branding') },
      { label: translate('page.platformTenantApps.field.configJson'), name: 'configJson', rows: 3, span: 2, type: 'textarea', section: translate('page.platformTenantApps.section.extendedConfig') },
      { label: translate('page.platformTenantApps.field.remark'), name: 'remark', rows: 3, span: 2, type: 'textarea', section: translate('page.platformTenantApps.section.remark') }
    ],
    [translate]
  );

  return (
    <PlatformResourcePage
      api={{ create: createTenantApp, delete: deleteTenantApp, list: getTenantApps, update: updateTenantApp }}
      columnSettingsKey="platform-tenant-apps"
      columns={columns}
      defaultFormState={defaultFormState}
      defaultSearchState={{ keyword: '', status: '' }}
      description={translate('page.platformTenantApps.description')}
      fields={fields}
      getDisplayName={(item) => `${item.tenantName} / ${item.appName}`}
      itemName={translate('page.platformTenantApps.itemName')}
      permissionCodes={{ add: 'platform:tenant-app:install', delete: 'platform:tenant-app:uninstall', edit: 'platform:tenant-app:install' }}
      queryKeyPrefix={queryKeys.platform.tenantAppsRoot() as unknown as string[]}
      title={translate('page.platformTenantApps.title')}
    />
  );
}
