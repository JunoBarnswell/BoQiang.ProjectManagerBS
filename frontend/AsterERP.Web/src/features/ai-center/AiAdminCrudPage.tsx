import { useMemo, type ReactNode } from 'react';

import type { GridPageResult } from '../../api/shared.types';
import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { formatMessage } from '../../core/i18n/formatMessage';
import { useI18n } from '../../core/i18n/I18nProvider';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import type { CrudApi, CrudListQuery } from '../../shared/components/crud-page/useCrudResource';
import { useCrudResource } from '../../shared/components/crud-page/useCrudResource';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import { ModalForm } from '../../shared/forms/ModalForm';
import { SearchForm } from '../../shared/forms/SearchForm';
import { DataTable } from '../../shared/table/DataTable';
import { TableActions } from '../../shared/table/TableActions';
import type { DataTableColumn } from '../../shared/table/tableTypes';

import type { AiGridQuery } from './api/aiCenter.api';

import './styles/ai-center.css';

export interface AiAdminSearchState {
  keyword: string;
  status: string;
}

interface AiAdminCrudPageProps<TItem extends { id: string }, TRequest extends object> {
  columns: DataTableColumn<TItem>[];
  createPermission: string;
  defaultFormState: TRequest;
  description: string;
  editPermission: string;
  extraRowActions?: (row: TItem) => ReactNode;
  fields: FormFieldConfig<TRequest>[];
  itemName: string;
  list: (query: AiGridQuery, signal?: AbortSignal) => Promise<ApiEnvelope<GridPageResult<TItem>>>;
  mapToFormState: (item: TItem) => TRequest;
  mutationApi: Omit<CrudApi<TItem, TRequest, TRequest, AiAdminSearchState>, 'list'>;
  title: string;
}

const defaultSearchState: AiAdminSearchState = {
  keyword: '',
  status: ''
};

export function AiAdminCrudPage<TItem extends { id: string }, TRequest extends object>({
  columns,
  createPermission,
  defaultFormState,
  description,
  editPermission,
  extraRowActions,
  fields,
  itemName,
  list,
  mapToFormState,
  mutationApi,
  title
}: AiAdminCrudPageProps<TItem, TRequest>) {
  const { translate } = useI18n();
  const searchFields: FormFieldConfig<AiAdminSearchState>[] = useMemo(
    () => [
      {
        label: translate('ai.search.keyword'),
        name: 'keyword',
        placeholder: translate('ai.search.keywordPlaceholder'),
        type: 'text'
      },
      {
        label: translate('ai.search.status'),
        name: 'status',
        options: [
          { label: translate('ai.search.statusAll'), value: '' },
          { label: translate('ai.search.statusEnabled'), value: 'Enabled' },
          { label: translate('ai.search.statusDisabled'), value: 'Disabled' }
        ],
        type: 'select'
      }
    ],
    [translate]
  );

  const resourceApi = useMemo<CrudApi<TItem, TRequest, TRequest, AiAdminSearchState>>(
    () => ({
      ...mutationApi,
      list: (query: CrudListQuery<AiAdminSearchState>, signal?: AbortSignal) =>
        list(
          {
            keyword: query.keyword,
            pageIndex: query.pageIndex,
            pageSize: query.pageSize,
            status: query.status
          },
          signal
        )
    }),
    [list, mutationApi]
  );

  const resource = useCrudResource<TItem, TRequest, TRequest, AiAdminSearchState>({
    api: resourceApi,
    defaultFormState,
    defaultPageSize: 20,
    defaultSearchState,
    getId: (item) => item.id,
    itemName,
    queryKeyPrefix: ['ai', 'admin', title]
  });

  const rows = resource.listQuery.data?.data.items ?? [];
  const total = resource.listQuery.data?.data.total ?? 0;

  return (
    <CrudPage
      actions={
        <PermissionButton className="primary-button" code={createPermission} type="button" onClick={resource.handleCreate}>
          {formatMessage(translate('ai.actions.create'), { itemName })}
        </PermissionButton>
      }
      className="ai-chat-page"
      description={description}
      eyebrow={translate('ai.eyebrow')}
      searchArea={
        <SearchForm
          fields={searchFields}
          loading={resource.listQuery.isFetching}
          onReset={resource.handleReset}
          onSubmit={(value) => resource.handleSearch(value)}
          onValueChange={(value) => resource.setSearchDraft(value)}
          value={resource.searchDraft}
        />
      }
      title={title}
    >
      <DataTable
        className="ai-admin-grid"
        columns={columns}
        fitScreen
        loading={resource.listQuery.isFetching}
        onPageChange={resource.setPageIndex}
        onPageSizeChange={resource.setPageSize}
        pagination={{
          current: resource.pageIndex,
          pageSize: resource.pageSize,
          total
        }}
        rowActions={(row) => (
          <TableActions>
            {extraRowActions?.(row)}
            <PermissionButton
              className="ghost-button"
              code={editPermission}
              fallback="disable"
              type="button"
              onClick={() => resource.handleEdit(row, mapToFormState)}
            >
              {translate('ai.actions.edit')}
            </PermissionButton>
            <PermissionButton
              className="ghost-button"
              code={editPermission}
              fallback="disable"
              type="button"
              onClick={() => resource.handleDelete(row, resolveRowName(row))}
            >
              {translate('ai.actions.delete')}
            </PermissionButton>
          </TableActions>
        )}
        rowKey={(row) => row.id}
        rows={rows}
      />

      <ModalForm
        actions={[
          {
            label: translate('common.cancel'),
            onClick: () => resource.setModalOpen(false),
            variant: 'ghost'
          },
          {
            label: translate('common.save'),
            loading: resource.createMutation.isPending || resource.updateMutation.isPending,
            onClick: () => void resource.handleSubmit(),
            type: 'button',
            variant: 'primary'
          }
        ]}
        fields={fields}
        open={resource.modalOpen}
        onClose={() => resource.setModalOpen(false)}
        onValueChange={(name, value) =>
          resource.setFormState((current) => ({
            ...current,
            [name]: value
          }))
        }
        title={resource.editingId ? formatMessage(translate('ai.modal.edit'), { itemName }) : formatMessage(translate('ai.modal.create'), { itemName })}
        value={resource.formState}
      />
    </CrudPage>
  );
}

function resolveRowName(row: { id: string } & Record<string, unknown>): string {
  return String(row.providerName ?? row.displayName ?? row.templateName ?? row.agentName ?? row.title ?? row.id);
}
