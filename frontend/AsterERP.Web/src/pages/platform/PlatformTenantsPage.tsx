import { useMemo } from 'react';

import { createTenant, deleteTenant, getTenants, updateTenant } from '../../api/platform/platform-management.api';
import type { TenantListItemDto, TenantUpsertRequest } from '../../api/platform/platform.types';
import { useI18n } from '../../core/i18n/I18nProvider';
import { queryKeys } from '../../core/query/queryKeys';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import type { DataTableColumn } from '../../shared/table/tableTypes';

import { PlatformResourcePage } from './PlatformResourcePage';

const defaultFormState: TenantUpsertRequest = {
  configJson: '',
  contactName: '',
  contactPhone: '',
  expiredAt: null,
  remark: '',
  shortName: '',
  status: 'Enabled',
  tenantCode: '',
  tenantName: ''
};

export function PlatformTenantsPage() {
  const { translate } = useI18n();

  const statusFilterOptions = useMemo(
    () => [
      { label: translate('platform.common.enabled'), value: 'Enabled' },
      { label: translate('platform.common.disabled'), value: 'Disabled' }
    ],
    [translate]
  );

  const columns = useMemo<DataTableColumn<TenantListItemDto>[]>(
    () => [
      { key: 'tenantName', title: translate('page.platformTenants.field.tenantName'), responsivePriority: 100, sortable: true, filterable: true, filterType: 'text' },
      { key: 'tenantCode', title: translate('page.platformTenants.field.tenantCode'), width: '150px', responsivePriority: 95, sortable: true, filterable: true, filterType: 'text' },
      { key: 'shortName', title: translate('page.platformTenants.field.shortName'), width: '130px', render: (row) => row.shortName ?? '-', sortable: true, filterable: true, filterType: 'text' },
      { key: 'status', title: translate('page.platformTenants.field.status'), width: '90px', align: 'center', render: (row) => (row.status === 'Enabled' ? translate('platform.common.enabled') : translate('platform.common.disabled')), sortable: true, filterable: true, filterType: 'select', filterOptions: statusFilterOptions },
      { key: 'contactName', title: translate('page.platformTenants.field.contactName'), width: '120px', render: (row) => row.contactName ?? '-', sortable: true, filterable: true, filterType: 'text' },
      { key: 'contactPhone', title: translate('page.platformTenants.field.contactPhone'), width: '150px', render: (row) => row.contactPhone ?? '-', sortable: true, filterable: true, filterType: 'text' },
      { key: 'remark', title: translate('page.platformTenants.field.remark'), width: '180px', render: (row) => row.remark ?? '-', sortable: true, filterable: true, filterType: 'text' }
    ],
    [statusFilterOptions, translate]
  );

  const fields = useMemo<FormFieldConfig<TenantUpsertRequest>[]>(
    () => [
      { label: translate('page.platformTenants.field.tenantName'), name: 'tenantName', required: true, span: 1, type: 'text', section: translate('page.platformTenants.section.basicInfo') },
      { label: translate('page.platformTenants.field.tenantCode'), name: 'tenantCode', required: true, span: 1, type: 'text', section: translate('page.platformTenants.section.basicInfo') },
      { label: translate('page.platformTenants.field.shortName'), name: 'shortName', span: 1, type: 'text', section: translate('page.platformTenants.section.basicInfo') },
      { label: translate('page.platformTenants.field.status'), name: 'status', required: true, span: 1, type: 'select', options: [{ label: translate('platform.common.enabled'), value: 'Enabled' }, { label: translate('platform.common.disabled'), value: 'Disabled' }], section: translate('page.platformTenants.section.basicInfo') },
      { label: translate('page.platformTenants.field.contactName'), name: 'contactName', span: 1, type: 'text', section: translate('page.platformTenants.section.contactInfo') },
      { label: translate('page.platformTenants.field.contactPhone'), name: 'contactPhone', span: 1, type: 'text', section: translate('page.platformTenants.section.contactInfo') },
      { label: translate('page.platformTenants.field.configJson'), name: 'configJson', rows: 3, span: 2, type: 'textarea', section: translate('page.platformTenants.section.extendedConfig') },
      { label: translate('page.platformTenants.field.remark'), name: 'remark', rows: 3, span: 2, type: 'textarea', section: translate('page.platformTenants.section.remark') }
    ],
    [translate]
  );

  return (
    <PlatformResourcePage
      api={{ create: createTenant, delete: deleteTenant, list: getTenants, update: updateTenant }}
      columnSettingsKey="platform-tenants"
      columns={columns}
      defaultFormState={defaultFormState}
      defaultSearchState={{ keyword: '', status: '' }}
      description={translate('page.platformTenants.description')}
      fields={fields}
      getDisplayName={(item) => item.tenantName}
      itemName={translate('page.platformTenants.itemName')}
      permissionCodes={{ add: 'platform:tenant:add', delete: 'platform:tenant:delete', edit: 'platform:tenant:edit' }}
      queryKeyPrefix={queryKeys.platform.tenantsRoot() as unknown as string[]}
      title={translate('page.platformTenants.title')}
    />
  );
}
