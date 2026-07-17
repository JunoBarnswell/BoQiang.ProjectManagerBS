import { useMemo } from 'react';

import { createUserAppRole, deleteUserAppRole, getUserAppRoles, updateUserAppRole } from '../../api/platform/platform-management.api';
import type { UserAppRoleDto, UserAppRoleUpsertRequest } from '../../api/platform/platform.types';
import { useI18n } from '../../core/i18n/I18nProvider';
import { queryKeys } from '../../core/query/queryKeys';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import type { DataTableColumn } from '../../shared/table/tableTypes';

import { PlatformResourcePage } from './PlatformResourcePage';

const defaultFormState: UserAppRoleUpsertRequest = {
  appCode: '',
  isDefault: false,
  remark: '',
  roleId: '',
  tenantId: '',
  userId: ''
};

export function PlatformUserAppRolesPage() {
  const { translate } = useI18n();

  const columns = useMemo<DataTableColumn<UserAppRoleDto>[]>(
    () => [
      { key: 'displayName', title: translate('page.platformUserAppRoles.field.user'), responsivePriority: 100, render: (row) => `${row.displayName} (${row.userName})`, sortable: true, filterable: true, filterType: 'text' },
      { key: 'tenantName', title: translate('page.platformUserAppRoles.field.tenantName'), responsivePriority: 95, sortable: true, filterable: true, filterType: 'text' },
      { key: 'appName', title: translate('page.platformUserAppRoles.field.appName'), width: '150px', sortable: true, filterable: true, filterType: 'text' },
      { key: 'appCode', title: translate('page.platformUserAppRoles.field.appCode'), width: '100px', sortable: true, filterable: true, filterType: 'text' },
      { key: 'roleName', title: translate('page.platformUserAppRoles.field.roleName'), width: '160px', sortable: true, filterable: true, filterType: 'text' },
      { key: 'isDefault', title: translate('page.platformUserAppRoles.field.isDefault'), width: '80px', align: 'center', render: (row) => (row.isDefault ? translate('common.yes') : translate('common.no')), sortable: true, filterable: true, filterType: 'boolean' },
      { key: 'remark', title: translate('page.platformUserAppRoles.field.remark'), width: '180px', render: (row) => row.remark ?? '-', sortable: true, filterable: true, filterType: 'text' }
    ],
    [translate]
  );

  const fields = useMemo<FormFieldConfig<UserAppRoleUpsertRequest>[]>(
    () => [
      { label: translate('page.platformUserAppRoles.field.userId'), name: 'userId', required: true, span: 1, type: 'text', section: translate('page.platformUserAppRoles.section.authorization') },
      { label: translate('page.platformUserAppRoles.field.tenantId'), name: 'tenantId', required: true, span: 1, type: 'text', section: translate('page.platformUserAppRoles.section.authorization') },
      { label: translate('page.platformUserAppRoles.field.appCode'), name: 'appCode', required: true, span: 1, type: 'text', section: translate('page.platformUserAppRoles.section.authorization') },
      { label: translate('page.platformUserAppRoles.field.roleId'), name: 'roleId', required: true, span: 1, type: 'text', section: translate('page.platformUserAppRoles.section.authorization') },
      { label: translate('page.platformUserAppRoles.field.isDefault'), name: 'isDefault', span: 1, type: 'checkbox', section: translate('page.platformUserAppRoles.section.authorization') },
      { label: translate('page.platformUserAppRoles.field.remark'), name: 'remark', rows: 3, span: 2, type: 'textarea', section: translate('page.platformUserAppRoles.section.remark') }
    ],
    [translate]
  );

  return (
    <PlatformResourcePage
      api={{ create: createUserAppRole, delete: deleteUserAppRole, list: getUserAppRoles, update: updateUserAppRole }}
      columnSettingsKey="platform-user-app-roles"
      columns={columns}
      defaultFormState={defaultFormState}
      defaultSearchState={{ keyword: '', status: '' }}
      description={translate('page.platformUserAppRoles.description')}
      fields={fields}
      getDisplayName={(item) => `${item.displayName} / ${item.appName} / ${item.roleName}`}
      itemName={translate('page.platformUserAppRoles.itemName')}
      permissionCodes={{ add: 'platform:user-app-role:edit', delete: 'platform:user-app-role:edit', edit: 'platform:user-app-role:edit' }}
      queryKeyPrefix={queryKeys.platform.userAppRolesRoot() as unknown as string[]}
      title={translate('page.platformUserAppRoles.title')}
    />
  );
}
