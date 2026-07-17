import { useMemo } from 'react';

import { createUserTenant, deleteUserTenant, getUserTenants, updateUserTenant } from '../../api/platform/platform-management.api';
import type { UserTenantMembershipDto, UserTenantMembershipUpsertRequest } from '../../api/platform/platform.types';
import { useI18n } from '../../core/i18n/I18nProvider';
import { queryKeys } from '../../core/query/queryKeys';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import type { DataTableColumn } from '../../shared/table/tableTypes';

import { PlatformResourcePage } from './PlatformResourcePage';

const defaultFormState: UserTenantMembershipUpsertRequest = {
  deptId: '',
  isDefault: false,
  isTenantAdmin: false,
  positionId: '',
  remark: '',
  status: 'Enabled',
  tenantId: '',
  userId: ''
};

export function PlatformUserTenantsPage() {
  const { translate } = useI18n();

  const statusFilterOptions = useMemo(
    () => [
      { label: translate('platform.common.enabled'), value: 'Enabled' },
      { label: translate('platform.common.disabled'), value: 'Disabled' }
    ],
    [translate]
  );

  const columns = useMemo<DataTableColumn<UserTenantMembershipDto>[]>(
    () => [
      { key: 'displayName', title: translate('page.platformUserTenants.field.user'), responsivePriority: 100, render: (row) => `${row.displayName} (${row.userName})`, sortable: true, filterable: true, filterType: 'text' },
      { key: 'tenantName', title: translate('page.platformUserTenants.field.tenantName'), responsivePriority: 95, sortable: true, filterable: true, filterType: 'text' },
      { key: 'deptName', title: translate('page.platformUserTenants.field.deptName'), width: '140px', render: (row) => row.deptName ?? '-', sortable: true, filterable: true, filterType: 'text' },
      { key: 'positionName', title: translate('page.platformUserTenants.field.positionName'), width: '140px', render: (row) => row.positionName ?? '-', sortable: true, filterable: true, filterType: 'text' },
      { key: 'isTenantAdmin', title: translate('page.platformUserTenants.field.isTenantAdmin'), width: '110px', align: 'center', render: (row) => (row.isTenantAdmin ? translate('common.yes') : translate('common.no')), sortable: true, filterable: true, filterType: 'boolean' },
      { key: 'isDefault', title: translate('page.platformUserTenants.field.isDefault'), width: '80px', align: 'center', render: (row) => (row.isDefault ? translate('common.yes') : translate('common.no')), sortable: true, filterable: true, filterType: 'boolean' },
      { key: 'status', title: translate('page.platformUserTenants.field.status'), width: '90px', align: 'center', render: (row) => (row.status === 'Enabled' ? translate('platform.common.enabled') : translate('platform.common.disabled')), sortable: true, filterable: true, filterType: 'select', filterOptions: statusFilterOptions }
    ],
    [statusFilterOptions, translate]
  );

  const fields = useMemo<FormFieldConfig<UserTenantMembershipUpsertRequest>[]>(
    () => [
      { label: translate('page.platformUserTenants.field.userId'), name: 'userId', required: true, span: 1, type: 'text', section: translate('page.platformUserTenants.section.relationship') },
      { label: translate('page.platformUserTenants.field.tenantId'), name: 'tenantId', required: true, span: 1, type: 'text', section: translate('page.platformUserTenants.section.relationship') },
      { label: translate('page.platformUserTenants.field.deptId'), name: 'deptId', span: 1, type: 'text', section: translate('page.platformUserTenants.section.organization') },
      { label: translate('page.platformUserTenants.field.positionId'), name: 'positionId', span: 1, type: 'text', section: translate('page.platformUserTenants.section.organization') },
      { label: translate('page.platformUserTenants.field.isTenantAdmin'), name: 'isTenantAdmin', span: 1, type: 'checkbox', section: translate('page.platformUserTenants.section.permission') },
      { label: translate('page.platformUserTenants.field.isDefault'), name: 'isDefault', span: 1, type: 'checkbox', section: translate('page.platformUserTenants.section.permission') },
      { label: translate('page.platformUserTenants.field.status'), name: 'status', required: true, span: 1, type: 'select', options: [{ label: translate('platform.common.enabled'), value: 'Enabled' }, { label: translate('platform.common.disabled'), value: 'Disabled' }], section: translate('page.platformUserTenants.section.status') },
      { label: translate('page.platformUserTenants.field.remark'), name: 'remark', rows: 3, span: 2, type: 'textarea', section: translate('page.platformUserTenants.section.remark') }
    ],
    [translate]
  );

  return (
    <PlatformResourcePage
      api={{ create: createUserTenant, delete: deleteUserTenant, list: getUserTenants, update: updateUserTenant }}
      columnSettingsKey="platform-user-tenants"
      columns={columns}
      defaultFormState={defaultFormState}
      defaultSearchState={{ keyword: '', status: '' }}
      description={translate('page.platformUserTenants.description')}
      fields={fields}
      getDisplayName={(item) => `${item.displayName} / ${item.tenantName}`}
      itemName={translate('page.platformUserTenants.itemName')}
      permissionCodes={{ add: 'platform:user-tenant:edit', delete: 'platform:user-tenant:edit', edit: 'platform:user-tenant:edit' }}
      queryKeyPrefix={queryKeys.platform.userTenantsRoot() as unknown as string[]}
      title={translate('page.platformUserTenants.title')}
    />
  );
}
