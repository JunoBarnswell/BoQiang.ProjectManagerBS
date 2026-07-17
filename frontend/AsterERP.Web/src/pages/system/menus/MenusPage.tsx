import { useQueryClient } from '@tanstack/react-query';
import { useMemo, type SetStateAction } from 'react';

import {
  batchDeleteMenus,
  batchUpdateMenuStatus,
  createMenu,
  deleteMenu,
  getMenu,
  getMenuTree,
  getMenus,
  updateMenu
} from '../../../api/system/system-management.api';
import type { MenuListItemDto, MenuTreeNodeDto, MenuUpsertRequest } from '../../../api/system/system.types';
import { flattenMenuNodes, flattenMenuTree } from '../../../core/auth/menuUtils';
import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { STATIC_LOOKUP_STALE_TIME_MS } from '../../../core/query/cacheDurations';
import { queryKeys } from '../../../core/query/queryKeys';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { useWorkspaceStore } from '../../../core/state';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { CrudPage } from '../../../shared/components/crud-page/CrudPage';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import type { FormFieldConfig, FormOption } from '../../../shared/forms/formTypes';
import { ModalForm } from '../../../shared/forms/ModalForm';
import { SearchForm } from '../../../shared/forms/SearchForm';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { DataTable } from '../../../shared/table/DataTable';
import { TableActions } from '../../../shared/table/TableActions';
import type { DataTableColumn, DataTableQueryState, DataTableSortRule } from '../../../shared/table/tableTypes';
import { useTabPageState } from '../../../shared/tabs/useTabPageState';
import { TreeFilterPanel } from '../../../shared/tree/TreeFilterPanel';
import { getErrorMessage } from '../../../shared/utils/errorMessage';

interface MenuSearchState {
  appCode: string;
  keyword: string;
  keywordDraft: string;
  menuType: string;
  status: string;
  tenantId: string;
}

type MenuFormState = MenuUpsertRequest;

interface MenusPageState {
  editingId: string | null;
  formState: MenuFormState;
  isModalOpen: boolean;
  pageIndex: number;
  pageSize: number;
  searchState: MenuSearchState;
  selectedTreeMenuCode: string;
  selectedRowKeys: string[];
  sorts: DataTableSortRule[];
  tableQuery: DataTableQueryState;
  treeSearchKeyword: string;
}

const defaultFormState: MenuFormState = {
  appCode: '',
  componentName: '',
  configJson: '',
  icon: '',
  menuCode: '',
  menuName: '',
  menuType: 'Menu',
  pageCode: '',
  artifactId: '',
  parentCode: '',
  permissionCode: '',
  remark: '',
  routePath: '',
  scopeType: 'Tenant',
  sortOrder: 1,
  tenantId: '',
  visible: true
};

const defaultPageSize = 10;
const defaultTableQuery: DataTableQueryState = { conditions: [], matchMode: 'and' };

function buildMenuOptions(menuTree: MenuTreeNodeDto[], translate: (key: string) => string): FormOption[] {
  const nodes = flattenMenuNodes(flattenMenuTree(menuTree)).filter((node) => node.menuType !== 'Button');
  return [{ label: translate('page.systemMenus.rootNode'), value: '' }, ...nodes.map((node) => ({ label: `${node.menuName} (${node.menuCode})`, value: node.menuCode }))];
}

function mapMenuTypeLabel(menuType: string, translate: (key: string) => string): string {
  switch (menuType) {
    case 'Directory':
      return translate('page.systemMenus.type.directory');
    case 'Menu':
      return translate('page.systemMenus.type.menu');
    case 'Button':
      return translate('page.systemMenus.type.button');
    default:
      return menuType;
  }
}

export function MenusPage() {
  const { translate } = useI18n();
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const currentWorkspace = useWorkspaceStore((state) => state.currentWorkspace);
  const defaultTenantId = currentWorkspace?.tenantId ?? '';
  const defaultAppCode = currentWorkspace?.appCode ?? '';
  const [pageState, setPageState] = useTabPageState<MenusPageState>(
    {
      editingId: null,
      formState: { ...defaultFormState, appCode: defaultAppCode, tenantId: defaultTenantId },
      isModalOpen: false,
      pageIndex: 1,
      pageSize: defaultPageSize,
      searchState: { appCode: defaultAppCode, keyword: '', keywordDraft: '', menuType: '', status: '', tenantId: defaultTenantId },
      selectedTreeMenuCode: '',
      selectedRowKeys: [],
      sorts: [],
      tableQuery: defaultTableQuery,
      treeSearchKeyword: ''
    },
    { cacheKey: 'menus-page' }
  );
  const { editingId, formState, isModalOpen, pageIndex, pageSize, searchState, selectedTreeMenuCode, selectedRowKeys, sorts, tableQuery, treeSearchKeyword } = pageState;
  const setPageField = <K extends keyof MenusPageState>(key: K, value: SetStateAction<MenusPageState[K]>) => {
    setPageState((current) => ({
      ...current,
      [key]: typeof value === 'function' ? (value as (previous: MenusPageState[K]) => MenusPageState[K])(current[key]) : value
    }));
  };
  const setSearchState = (value: SetStateAction<MenuSearchState>) => setPageField('searchState', value);
  const setSelectedRowKeys = (value: SetStateAction<string[]>) => setPageField('selectedRowKeys', value);
  const setIsModalOpen = (value: boolean) => setPageField('isModalOpen', value);
  const setEditingId = (value: string | null) => setPageField('editingId', value);
  const setFormState = (value: SetStateAction<MenuFormState>) => setPageField('formState', value);
  const setSelectedTreeMenuCode = (value: string) => setPageField('selectedTreeMenuCode', value);
  const setPageIndex = (value: number) => setPageField('pageIndex', value);
  const setPageSize = (value: number) => {
    setPageField('pageSize', value);
    setPageIndex(1);
  };
  const setSorts = (value: DataTableSortRule[]) => {
    setPageState((current) => ({ ...current, pageIndex: 1, sorts: value }));
  };
  const setTableQuery = (value: DataTableQueryState) => {
    setPageState((current) => ({ ...current, pageIndex: 1, tableQuery: value }));
  };
  const setTreeSearchKeyword = (value: string) => setPageField('treeSearchKeyword', value);

  const menuTreeQuery = useApiQuery({
    queryFn: () => getMenuTree({ appCode: searchState.appCode, tenantId: searchState.tenantId }),
    queryKey: queryKeys.systemManagement.menuTree(searchState.tenantId, searchState.appCode),
    staleTimeMs: STATIC_LOOKUP_STALE_TIME_MS
  });

  const menuListQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: () =>
      getMenus({
        includeDescendants: Boolean(selectedTreeMenuCode),
        appCode: searchState.appCode,
        filters: tableQuery.conditions,
        keyword: searchState.keyword,
        menuType: searchState.menuType,
        pageIndex,
        pageSize,
        parentId: selectedTreeMenuCode,
        status: searchState.status,
        tenantId: searchState.tenantId,
        sorts
      }),
    queryKey: [
      ...queryKeys.systemManagement.menus(
        pageIndex,
        pageSize,
        searchState.keyword,
        selectedTreeMenuCode,
        searchState.menuType,
        searchState.status,
        Boolean(selectedTreeMenuCode),
        searchState.tenantId,
        searchState.appCode,
        sorts
      ),
      tableQuery
    ]
  });

  const createMutation = useApiMutation({ mutationFn: (request: MenuUpsertRequest) => createMenu(request) });
  const updateMutation = useApiMutation({
    mutationFn: ({ id, request }: { id: string; request: MenuUpsertRequest }) => updateMenu(id, request)
  });
  const deleteMutation = useApiMutation({ mutationFn: (id: string) => deleteMenu(id) });
  const batchDeleteMutation = useApiMutation({ mutationFn: (ids: string[]) => batchDeleteMenus(ids) });
  const batchStatusMutation = useApiMutation({
    mutationFn: ({ ids, status }: { ids: string[]; status: string }) => batchUpdateMenuStatus(ids, status)
  });

  const parentOptions = useMemo(() => buildMenuOptions(menuTreeQuery.data?.data ?? [], translate), [menuTreeQuery.data?.data, translate]);

  const columns: DataTableColumn<MenuListItemDto>[] = useMemo(
    () => [
      { key: 'rowIndex', title: translate('page.systemMenus.column.index'), width: '70px', align: 'center', responsivePriority: 100, render: (_row, index) => (pageIndex - 1) * pageSize + index + 1 },
      { key: 'appCode', title: translate('page.systemMenus.column.appCode'), width: '90px', responsivePriority: 98, sortable: true, filterable: true, filterType: 'text' },
      { key: 'menuName', title: translate('page.systemMenus.column.menuName'), responsivePriority: 100, sortable: true, filterable: true, filterType: 'text' },
      {
        key: 'menuType',
        title: translate('page.systemMenus.column.menuType'),
        width: '100px',
        responsivePriority: 90,
        sortable: true,
        filterable: true,
        filterType: 'select',
        filterOperators: ['equals'],
        filterOptions: [
          { label: translate('page.systemMenus.type.directory'), value: 'Directory' },
          { label: translate('page.systemMenus.type.menu'), value: 'Menu' },
          { label: translate('page.systemMenus.type.button'), value: 'Button' }
        ],
        render: (row) => mapMenuTypeLabel(row.menuType, translate)
      },
      { key: 'pageCode', title: translate('page.systemMenus.column.pageCode'), width: '190px', hideBelow: 'lg', responsivePriority: 78, sortable: true, filterable: true, filterType: 'text', render: (row) => row.pageCode ?? '-' },
      { key: 'routePath', title: translate('page.systemMenus.column.routePath'), width: '180px', hideBelow: 'lg', responsivePriority: 75, sortable: true, filterable: true, filterType: 'text', render: (row) => row.routePath ?? '-' },
      { key: 'componentName', title: translate('page.systemMenus.column.componentName'), width: '180px', hideBelow: 'lg', responsivePriority: 70, sortable: true, filterable: true, filterType: 'text', render: (row) => row.componentName ?? '-' },
      { key: 'permissionCode', title: translate('page.systemMenus.column.permissionCode'), width: '180px', hideBelow: 'xl', responsivePriority: 65, sortable: true, filterable: true, filterType: 'text', render: (row) => row.permissionCode ?? '-' },
      { key: 'sortOrder', title: translate('page.systemMenus.column.sortOrder'), width: '90px', align: 'center', responsivePriority: 60, sortable: true, filterable: true, filterType: 'number' },
      {
        key: 'visible',
        title: translate('page.systemMenus.column.status'),
        width: '90px',
        align: 'center',
        sortable: true,
        filterable: true,
        filterType: 'boolean',
        filterOperators: ['equals'],
        filterOptions: [
          { label: translate('common.enabled'), value: true },
          { label: translate('common.disabled'), value: false }
        ],
        render: (row) => (row.visible ? translate('common.enabled') : translate('common.disabled'))
      }
    ],
    [pageIndex, pageSize, translate]
  );

  const formFields: FormFieldConfig<MenuFormState>[] = useMemo(
    () => [
      { label: translate('page.systemMenus.field.menuName'), name: 'menuName', placeholder: translate('page.systemMenus.placeholder.menuName'), required: true, span: 1, type: 'text', section: translate('page.systemMenus.section.basicInfo') },
      { label: translate('page.systemMenus.field.menuCode'), name: 'menuCode', placeholder: translate('page.systemMenus.placeholder.menuCode'), required: true, span: 1, type: 'text', section: translate('page.systemMenus.section.basicInfo') },
      { label: translate('page.systemMenus.field.tenantId'), name: 'tenantId', placeholder: translate('page.systemMenus.placeholder.tenantId'), required: true, span: 1, type: 'text', section: translate('page.systemMenus.section.workspace') },
      { label: translate('page.systemMenus.field.appCode'), name: 'appCode', placeholder: translate('page.systemMenus.placeholder.appCode'), required: true, span: 1, type: 'text', section: translate('page.systemMenus.section.workspace') },
      { label: translate('page.systemMenus.field.parentCode'), name: 'parentCode', options: parentOptions, span: 2, type: 'select', section: translate('page.systemMenus.section.basicConfig') },
      {
        label: translate('page.systemMenus.field.menuType'),
        name: 'menuType',
        options: [
          { label: translate('page.systemMenus.type.directory'), value: 'Directory' },
          { label: translate('page.systemMenus.type.menu'), value: 'Menu' },
          { label: translate('page.systemMenus.type.button'), value: 'Button' }
        ],
        required: true,
        span: 1,
        type: 'select',
        section: translate('page.systemMenus.section.basicConfig')
      },
      { label: translate('page.systemMenus.field.sortOrder'), name: 'sortOrder', required: true, span: 1, type: 'number', section: translate('page.systemMenus.section.basicConfig') },
      { label: translate('page.systemMenus.field.routePath'), name: 'routePath', placeholder: translate('page.systemMenus.placeholder.routePath'), span: 1, type: 'text', section: translate('page.systemMenus.section.basicConfig') },
      { label: translate('page.systemMenus.field.componentName'), name: 'componentName', placeholder: translate('page.systemMenus.placeholder.componentName'), span: 1, type: 'text', section: translate('page.systemMenus.section.basicConfig') },
      { label: translate('page.systemMenus.field.pageCode'), name: 'pageCode', placeholder: translate('page.systemMenus.placeholder.pageCode'), span: 1, type: 'text', section: translate('page.systemMenus.section.runtimeConfig') },
      { label: translate('page.systemMenus.field.artifactId'), name: 'artifactId', placeholder: translate('page.systemMenus.placeholder.artifactId'), span: 1, type: 'text', section: translate('page.systemMenus.section.runtimeConfig') },
      {
        label: translate('page.systemMenus.field.scopeType'),
        name: 'scopeType',
        options: [
          { label: translate('page.systemMenus.scope.system'), value: 'System' },
          { label: translate('page.systemMenus.scope.app'), value: 'App' },
          { label: translate('page.systemMenus.scope.tenant'), value: 'Tenant' },
          { label: translate('page.systemMenus.scope.user'), value: 'User' }
        ],
        span: 1,
        type: 'select',
        section: translate('page.systemMenus.section.runtimeConfig')
      },
      { label: translate('page.systemMenus.field.permissionCode'), name: 'permissionCode', placeholder: translate('page.systemMenus.placeholder.permissionCode'), span: 1, type: 'text', section: translate('page.systemMenus.section.permissionConfig') },
      { label: translate('page.systemMenus.field.icon'), name: 'icon', placeholder: translate('page.systemMenus.placeholder.icon'), span: 1, type: 'text', section: translate('page.systemMenus.section.permissionConfig') },
      {
        label: translate('page.systemMenus.field.status'),
        name: 'visible',
        options: [
          { label: translate('common.enabled'), value: 'true' },
          { label: translate('common.disabled'), value: 'false' }
        ],
        required: true,
        span: 2,
        type: 'select',
        section: translate('page.systemMenus.section.permissionConfig')
      },
      { label: translate('page.systemMenus.field.configJson'), name: 'configJson', placeholder: translate('page.systemMenus.placeholder.configJson'), rows: 3, span: 2, type: 'textarea', section: translate('page.systemMenus.section.runtimeConfig') },
      { label: translate('page.systemMenus.field.remark'), name: 'remark', placeholder: translate('page.systemMenus.placeholder.remark'), rows: 3, span: 2, type: 'textarea', section: translate('page.systemMenus.section.remark') }
    ],
    [parentOptions, translate]
  );

  const rows = menuListQuery.data?.data.items ?? [];
  const total = menuListQuery.data?.data.total ?? 0;

  const refreshMenus = async () => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.systemManagement.menusRoot() });
    await queryClient.invalidateQueries({ queryKey: queryKeys.systemManagement.menuTree(searchState.tenantId, searchState.appCode) });
    await queryClient.invalidateQueries({ queryKey: queryKeys.systemManagement.permissionTree(searchState.tenantId, searchState.appCode) });
  };

  const openCreateModal = (parentCode = '') => {
    setEditingId(null);
    setFormState({ ...defaultFormState, appCode: searchState.appCode || defaultAppCode, menuType: 'Menu', parentCode, tenantId: searchState.tenantId || defaultTenantId });
    setIsModalOpen(true);
  };

  const openEditModal = async (row: MenuListItemDto) => {
    try {
      const response = await getMenu(row.id);
      const detail = response.data;
      setEditingId(detail.id);
      setFormState({
        appCode: detail.appCode,
        componentName: detail.componentName ?? '',
        configJson: detail.configJson ?? '',
        icon: detail.icon ?? '',
        menuCode: detail.menuCode,
        menuName: detail.menuName,
        menuType: detail.menuType,
        pageCode: detail.pageCode ?? '',
        artifactId: detail.artifactId ?? '',
        parentCode: detail.parentCode ?? '',
        permissionCode: detail.permissionCode ?? '',
        remark: detail.remark ?? '',
        routePath: detail.routePath ?? '',
        scopeType: detail.scopeType ?? 'Tenant',
        sortOrder: detail.sortOrder,
        tenantId: detail.tenantId,
        visible: detail.visible
      });
      setIsModalOpen(true);
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemMenus.error.loadDetailFailed')));
    }
  };

  const handleSave = async () => {
    const request: MenuUpsertRequest = {
      appCode: formState.appCode?.trim().toUpperCase() ?? '',
      componentName: formState.componentName?.trim() ?? '',
      configJson: formState.configJson?.trim() ?? '',
      icon: formState.icon?.trim() ?? '',
      menuCode: formState.menuCode.trim(),
      menuName: formState.menuName.trim(),
      menuType: formState.menuType,
      pageCode: formState.pageCode?.trim() ?? '',
      artifactId: formState.artifactId?.trim() ?? '',
      parentCode: formState.parentCode?.trim() ?? '',
      permissionCode: formState.permissionCode?.trim() ?? '',
      remark: formState.remark?.trim() ?? '',
      routePath: formState.routePath?.trim() ?? '',
      scopeType: formState.scopeType?.trim() ?? '',
      sortOrder: Number(formState.sortOrder),
      tenantId: formState.tenantId?.trim() ?? '',
      visible: Boolean(formState.visible)
    };

    if (!request.menuCode || !request.menuName || !request.menuType || !request.tenantId || !request.appCode) {
      message.error(translate('page.systemMenus.error.completeInfo'));
      return;
    }

    try {
      if (editingId) {
        await updateMutation.mutateAsync({ id: editingId, request });
      } else {
        await createMutation.mutateAsync(request);
      }

      setIsModalOpen(false);
      await refreshMenus();
      message.success(editingId ? translate('page.systemMenus.success.update') : translate('page.systemMenus.success.create'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemMenus.error.saveFailed')));
    }
  };

  const handleChangeStatus = async (ids: string[], status: 'Enabled' | 'Disabled') => {
    if (ids.length === 0) {
      message.error(translate('page.systemMenus.error.selectToOperate'));
      return;
    }

    try {
      await batchStatusMutation.mutateAsync({ ids, status });
      setSelectedRowKeys((current) => current.filter((id) => !ids.includes(id)));
      await refreshMenus();
      message.success(status === 'Enabled' ? translate('page.systemMenus.success.enabled') : translate('page.systemMenus.success.disabled'));
    } catch (error) {
      message.error(getErrorMessage(error, status === 'Enabled' ? translate('page.systemMenus.error.enableFailed') : translate('page.systemMenus.error.disableFailed')));
    }
  };

  const handleDelete = async (row: MenuListItemDto) => {
    confirm({
      title: translate('page.systemMenus.confirm.deleteTitle'),
      content: formatMessage(translate('page.systemMenus.confirm.deleteContent'), { name: row.menuName }),
      onConfirm: async () => {
        try {
          await deleteMutation.mutateAsync(row.id);
          await refreshMenus();
          message.success(translate('page.systemMenus.success.delete'));
        } catch (error) {
          message.error(getErrorMessage(error, translate('page.systemMenus.error.deleteFailed')));
        }
      }
    });
  };

  const handleBatchDelete = async () => {
    if (selectedRowKeys.length === 0) {
      message.error(translate('page.systemMenus.error.selectToDelete'));
      return;
    }

    const targetIds = [...selectedRowKeys];
    confirm({
      title: translate('page.systemMenus.confirm.deleteTitle'),
      content: formatMessage(translate('page.systemMenus.confirm.batchDeleteContent'), { count: targetIds.length }),
      onConfirm: async () => {
        try {
          await batchDeleteMutation.mutateAsync(targetIds);
          setSelectedRowKeys([]);
          await refreshMenus();
          message.success(formatMessage(translate('page.systemMenus.success.batchDelete'), { count: targetIds.length }));
        } catch (error) {
          message.error(getErrorMessage(error, translate('page.systemMenus.error.batchDeleteFailed')));
        }
      }
    });
  };

  const actionNode = (
    <div className="flex items-center gap-2">
      {selectedRowKeys.length > 0 && (
        <div className="mr-2 flex items-center gap-1 border-r pr-2 border-gray-300">
          <span className="text-xs text-gray-500 mr-2">{formatMessage(translate('common.selectedCount'), { count: selectedRowKeys.length })}</span>
          <PermissionButton code="system:menu:edit" className="text-primary-600 hover:bg-primary-50 px-2 py-1 rounded text-xs transition-colors" type="button" onClick={() => void handleChangeStatus(selectedRowKeys, 'Enabled')}>
            {translate('page.systemMenus.action.enable')}
          </PermissionButton>
          <PermissionButton code="system:menu:edit" className="text-primary-600 hover:bg-primary-50 px-2 py-1 rounded text-xs transition-colors" type="button" onClick={() => void handleChangeStatus(selectedRowKeys, 'Disabled')}>
            {translate('page.systemMenus.action.disable')}
          </PermissionButton>
          <PermissionButton code="system:menu:delete" className="text-red-600 hover:bg-red-50 px-2 py-1 rounded text-xs transition-colors" type="button" onClick={() => void handleBatchDelete()}>
            {translate('common.delete')}
          </PermissionButton>
        </div>
      )}
      <button className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50 hover:text-primary-600 flex items-center gap-1 shadow-sm transition-colors" type="button" onClick={() => void refreshMenus()}>
        <AppIcon name="arrows-clockwise" /> {translate('common.refresh')}
      </button>
      <PermissionButton code="system:menu:add" className="bg-primary-600 text-white px-3 py-1.5 rounded text-sm hover:bg-primary-700 flex items-center gap-1 shadow-sm font-medium transition-colors" type="button" onClick={() => openCreateModal()}>
        <AppIcon name="plus" /> {formatMessage(translate('platform.actions.create'), { itemName: translate('page.systemMenus.itemName') })}
      </PermissionButton>
    </div>
  );

  const searchNode = (
    <SearchForm
      fields={[
        { label: translate('page.systemMenus.field.tenantId'), name: 'tenantId', placeholder: translate('page.systemMenus.placeholder.tenantIdSearch'), type: 'text' },
        { label: translate('page.systemMenus.field.appCode'), name: 'appCode', placeholder: translate('page.systemMenus.placeholder.appCodeSearch'), type: 'text' },
        { label: translate('page.systemMenus.field.keyword'), name: 'keywordDraft', placeholder: translate('page.systemMenus.placeholder.keyword'), type: 'text' },
        {
          emptyOptionLabel: translate('common.allTypes'),
          label: translate('page.systemMenus.field.menuType'),
          name: 'menuType',
          options: [
            { label: translate('page.systemMenus.type.directory'), value: 'Directory' },
            { label: translate('page.systemMenus.type.menu'), value: 'Menu' },
            { label: translate('page.systemMenus.type.button'), value: 'Button' }
          ],
          type: 'select'
        },
        {
          emptyOptionLabel: translate('platform.search.allStatus'),
          label: translate('page.systemMenus.field.status'),
          name: 'status',
          options: [
            { label: translate('common.enabled'), value: 'Enabled' },
            { label: translate('common.disabled'), value: 'Disabled' }
          ],
          type: 'select'
        }
      ]}
      onReset={() => {
        setPageIndex(1);
        setSelectedTreeMenuCode('');
        setTableQuery(defaultTableQuery);
        setSearchState({ appCode: defaultAppCode, keyword: '', keywordDraft: '', menuType: '', status: '', tenantId: defaultTenantId });
      }}
      onSubmit={(value) => {
        setSelectedTreeMenuCode('');
        setPageIndex(1);
        setSearchState((current) => ({
          ...current,
          ...value,
          appCode: value.appCode.trim().toUpperCase(),
          keyword: value.keywordDraft.trim(),
          tenantId: value.tenantId.trim()
        }));
      }}
      onValueChange={(value) => setSearchState((current) => ({ ...current, ...value }))}
      value={searchState}
    />
  );

  return (
      <CrudPage title={translate('page.systemMenus.title')} description={translate('page.systemMenus.description')} actions={actionNode} searchArea={searchNode}>
      <div className="flex-1 flex gap-3 h-full overflow-hidden">
        <div className="w-56 bg-white border border-gray-200 rounded-lg shadow-sm flex flex-col shrink-0">
          <TreeFilterPanel
            emptyText={translate('page.systemMenus.tree.empty')}
            error={menuTreeQuery.isError}
            errorText={translate('page.systemMenus.tree.error')}
            getKey={(node) => node.menuCode}
            getLabel={(node) => node.menuName}
            getMeta={(node) => mapMenuTypeLabel(node.menuType, translate)}
            getSearchText={(node) => `${node.menuName} ${node.menuCode}`}
            loading={menuTreeQuery.isLoading}
            nodes={menuTreeQuery.data?.data ?? []}
            placeholder={translate('page.systemMenus.tree.placeholder')}
            searchKeyword={treeSearchKeyword}
            selectedKey={selectedTreeMenuCode}
            onReset={() => {
              setSelectedTreeMenuCode('');
              setPageIndex(1);
            }}
            onSearchKeywordChange={setTreeSearchKeyword}
            onSelect={(key) => {
              setSelectedTreeMenuCode(key);
              setPageIndex(1);
            }}
          />
        </div>

        <div className="flex-1 flex flex-col h-full min-w-0 bg-white border border-gray-200 rounded-lg shadow-sm">
          <DataTable
            columnSettingsKey="system-menus"
            columns={columns}
            emptyText={menuListQuery.isError ? translate('page.systemMenus.error.loadFailed') : translate('common.empty')}
            fitScreen
            loading={menuListQuery.isLoading}
            onPageChange={setPageIndex}
            onPageSizeChange={setPageSize}
            onQueryChange={setTableQuery}
            onSortsChange={setSorts}
            pageSizeOptions={[10, 20, 50]}
            pagination={{ current: pageIndex, pageSize, total }}
            rowActions={(row) => (
              <TableActions>
                <PermissionButton className="hover:text-primary-600 transition-colors" code="system:menu:add" title={translate('page.systemMenus.action.addChild')} type="button" onClick={() => openCreateModal(row.menuCode)}>
                  <AppIcon className="text-base" name="plus" />
                </PermissionButton>
                <PermissionButton className="hover:text-primary-600 transition-colors" code="system:menu:edit" title={translate('common.edit')} type="button" onClick={() => void openEditModal(row)}>
                  <AppIcon className="text-base" name="pencil-simple" />
                </PermissionButton>
                <PermissionButton className="hover:text-red-600 transition-colors" code="system:menu:delete" title={translate('common.delete')} type="button" onClick={() => handleDelete(row)}>
                  <AppIcon className="text-base" name="trash" />
                </PermissionButton>
              </TableActions>
            )}
            rowKey={(row) => row.id}
            rows={rows}
            selection={{ selectedRowKeys, onChange: setSelectedRowKeys }}
            sorts={sorts}
            tableQuery={tableQuery}
          />
        </div>
      </div>

      <ModalForm
        actions={[
          { label: translate('common.cancel'), onClick: () => setIsModalOpen(false), variant: 'ghost' },
          { label: translate('common.save'), onClick: () => void handleSave(), type: 'button', variant: 'primary', loading: createMutation.isPending || updateMutation.isPending }
        ]}
        fields={formFields}
        open={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onValueChange={(name, value) =>
          setFormState((current) => ({
            ...current,
            [name]:
              name === 'visible'
                ? ((value === 'true') as MenuFormState[keyof MenuFormState])
                : (value as MenuFormState[keyof MenuFormState])
          }))
        }
        title={editingId ? formatMessage(translate('platform.modal.edit'), { itemName: translate('page.systemMenus.itemName') }) : formatMessage(translate('platform.modal.create'), { itemName: translate('page.systemMenus.itemName') })}
        value={formState}
      >
        {translate('page.systemMenus.modal.description')}
      </ModalForm>
    </CrudPage>
  );
}
