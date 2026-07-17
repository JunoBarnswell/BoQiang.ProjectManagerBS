import { useMemo, type ReactNode } from 'react';

import { formatMessage } from '../../core/i18n/formatMessage';
import { useI18n } from '../../core/i18n/I18nProvider';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import { type CrudApi, useCrudResource } from '../../shared/components/crud-page/useCrudResource';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import { ModalForm } from '../../shared/forms/ModalForm';
import { SearchForm } from '../../shared/forms/SearchForm';
import { AppIcon } from '../../shared/icons/AppIcon';
import { DataTable } from '../../shared/table/DataTable';
import { TableActions } from '../../shared/table/TableActions';
import type { DataTableColumn } from '../../shared/table/tableTypes';

interface PlatformSearchState {
  keyword?: string;
  status?: string;
}

interface PlatformResourcePageProps<TItem extends { id: string }, TRequest extends object, TSearch extends PlatformSearchState> {
  api: CrudApi<TItem, TRequest, TRequest, TSearch>;
  columnSettingsKey: string;
  columns: DataTableColumn<TItem>[];
  defaultFormState: TRequest;
  defaultSearchState: TSearch;
  description: string;
  fields: FormFieldConfig<TRequest>[];
  getDisplayName: (item: TItem) => string;
  itemName: string;
  permissionCodes: {
    add: string;
    delete: string;
    edit: string;
  };
  onAdd?: (resource: PlatformResourceController<TItem, TRequest, TSearch>) => void;
  onRow?: (row: TItem, index: number) => void;
  onRowDoubleClick?: (row: TItem, index: number) => void;
  queryKeyPrefix: readonly string[];
  renderSidePanel?: (resource: PlatformResourceController<TItem, TRequest, TSearch>) => ReactNode;
  renderExtraRowActions?: (row: TItem, resource: PlatformResourceController<TItem, TRequest, TSearch>) => ReactNode;
  rowClassName?: string | ((row: TItem, index: number) => string);
  text?: Partial<{
    actionCreate: string;
    actionDelete: string;
    actionEdit: string;
    actionRefresh: string;
    modalCreateTitle: string;
    modalEditTitle: string;
    searchAllStatus: string;
    searchKeywordLabel: string;
    searchKeywordPlaceholder: string;
    searchStatusLabel: string;
  }>;
  title: string;
}

export type PlatformResourceController<
  TItem extends { id: string },
  TRequest extends object,
  TSearch extends PlatformSearchState
> = ReturnType<typeof useCrudResource<TItem, TRequest, TRequest, TSearch>>;

export function PlatformResourcePage<TItem extends { id: string }, TRequest extends object, TSearch extends PlatformSearchState>({
  api,
  columnSettingsKey,
  columns,
  defaultFormState,
  defaultSearchState,
  description,
  fields,
  getDisplayName,
  itemName,
  permissionCodes,
  onAdd,
  onRow,
  onRowDoubleClick,
  queryKeyPrefix,
  renderSidePanel,
  renderExtraRowActions,
  rowClassName,
  text,
  title
}: PlatformResourcePageProps<TItem, TRequest, TSearch>) {
  const { translate } = useI18n();
  const resource = useCrudResource<TItem, TRequest, TRequest, TSearch>({
    api,
    defaultFormState,
    defaultSearchState,
    getId: (item) => item.id,
    itemName,
    queryKeyPrefix
  });

  const searchNode = (
    <SearchForm
      fields={[
        {
          label: text?.searchKeywordLabel ?? translate('platform.search.keyword'),
          name: 'keyword',
          placeholder: text?.searchKeywordPlaceholder ?? formatMessage(translate('platform.search.keywordPlaceholder'), { itemName }),
          type: 'text'
        },
        {
          emptyOptionLabel: text?.searchAllStatus ?? translate('platform.search.allStatus'),
          label: text?.searchStatusLabel ?? translate('platform.search.status'),
          name: 'status',
          options: [
            { label: translate('platform.common.enabled'), value: 'Enabled' },
            { label: translate('platform.common.disabled'), value: 'Disabled' }
          ],
          type: 'select'
        }
      ]}
      onReset={resource.handleReset}
      onSubmit={(value) => resource.handleSearch(value as TSearch)}
      onValueChange={(value) => resource.setSearchDraft(value as TSearch)}
      value={resource.searchDraft}
    />
  );

  const actionNode = (
    <div className="flex items-center gap-2">
      <button className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50 flex items-center gap-1" type="button" onClick={() => void resource.refresh()}>
        <AppIcon name="arrows-clockwise" /> {text?.actionRefresh ?? translate('platform.actions.refresh')}
      </button>
      <PermissionButton code={permissionCodes.add} className="bg-primary-600 text-white px-3 py-1.5 rounded text-sm hover:bg-primary-700 flex items-center gap-1" type="button" onClick={() => onAdd ? onAdd(resource) : resource.handleCreate()}>
        <AppIcon name="plus" /> {text?.actionCreate ?? formatMessage(translate('platform.actions.create'), { itemName })}
      </PermissionButton>
    </div>
  );

  const tableColumns = useMemo(() => columns, [columns]);

  return (
    <CrudPage title={title} description={description} actions={actionNode} searchArea={searchNode}>
      <div className="flex-1 flex flex-col h-full min-w-0 bg-white border border-gray-200 rounded-lg shadow-sm">
        <DataTable
          columnSettingsKey={columnSettingsKey}
          columns={tableColumns}
          emptyText={resource.listQuery.isError ? translate('page.systemFiles.loadFailed') : translate('table.emptyDefault')}
          fitScreen
          loading={resource.listQuery.isLoading}
          onPageChange={resource.setPageIndex}
          onPageSizeChange={resource.setPageSize}
          onQueryChange={resource.setTableQuery}
          onRow={onRow}
          onRowDoubleClick={onRowDoubleClick}
          onSortsChange={resource.setSorts}
          pageSizeOptions={[10, 20, 50]}
          pagination={{ current: resource.pageIndex, pageSize: resource.pageSize, total: resource.listQuery.data?.data.total ?? 0 }}
          rowActions={(row) => (
            <TableActions>
              {renderExtraRowActions?.(row, resource)}
              <PermissionButton code={permissionCodes.edit} className="hover:text-primary-600 transition-colors" title={text?.actionEdit ?? translate('platform.actions.edit')} type="button" onClick={() => resource.handleEdit(row, (item) => ({ ...item } as unknown as TRequest))}>
                <AppIcon className="text-base" name="pencil-simple" />
              </PermissionButton>
              <PermissionButton code={permissionCodes.delete} className="hover:text-red-600 transition-colors" title={text?.actionDelete ?? translate('platform.actions.delete')} type="button" onClick={() => void resource.handleDelete(row, getDisplayName(row))}>
                <AppIcon className="text-base" name="trash" />
              </PermissionButton>
            </TableActions>
          )}
          rowKey={(row) => row.id}
          rowClassName={rowClassName}
          rows={resource.listQuery.data?.data.items ?? []}
          sorts={resource.sorts}
          tableQuery={resource.tableQuery}
        />
      </div>

      <ModalForm
        actions={[
          { label: translate('common.cancel'), onClick: () => resource.setModalOpen(false), variant: 'ghost' },
          { label: translate('common.save'), onClick: () => void resource.handleSubmit(), type: 'button', variant: 'primary', loading: resource.createMutation.isPending || resource.updateMutation.isPending }
        ]}
        fields={fields}
        open={resource.modalOpen}
        onClose={() => resource.setModalOpen(false)}
        onValueChange={(name, value) => resource.setFormState((current) => ({ ...current, [name]: value } as TRequest))}
        title={resource.editingId ? text?.modalEditTitle ?? formatMessage(translate('platform.modal.edit'), { itemName }) : text?.modalCreateTitle ?? formatMessage(translate('platform.modal.create'), { itemName })}
        value={resource.formState}
      >
        {description}
      </ModalForm>
      {renderSidePanel?.(resource)}
    </CrudPage>
  );
}
