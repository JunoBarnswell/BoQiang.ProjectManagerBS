import { useMemo } from 'react';

import {
  systemAnnouncementApi,
  type AnnouncementEffectiveStatus,
  type SystemAnnouncementListItemDto,
  type SystemAnnouncementUpsertRequest
} from '../../../api/system/announcements.api';
import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { CrudPage } from '../../../shared/components/crud-page/CrudPage';
import { useCrudResource } from '../../../shared/components/crud-page/useCrudResource';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import type { FormFieldConfig } from '../../../shared/forms/formTypes';
import { ModalForm } from '../../../shared/forms/ModalForm';
import { SearchForm } from '../../../shared/forms/SearchForm';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { DataTable } from '../../../shared/table/DataTable';
import { TableActions } from '../../../shared/table/TableActions';
import type { DataTableColumn } from '../../../shared/table/tableTypes';
import { getErrorMessage } from '../../../shared/utils/errorMessage';

interface AnnouncementSearchState {
  keyword?: string;
  status?: AnnouncementEffectiveStatus | '';
}

interface AnnouncementFormState {
  title: string;
  content: string;
  expiresAt: string;
  announcementType: string;
  scope: string;
  priority: number;
  isPinned: boolean;
  remark: string;
}

function toLocalDateTimeInput(value?: string | null): string {
  if (!value) {
    return '';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '';
  }

  const offsetMs = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 16);
}

function toApiRequest(formState: AnnouncementFormState): SystemAnnouncementUpsertRequest {
  return {
    title: formState.title,
    content: formState.content,
    announcementType: formState.announcementType,
    scope: formState.scope,
    priority: formState.priority,
    isPinned: formState.isPinned,
    remark: formState.remark || null,
    expiresAt: formState.expiresAt ? new Date(formState.expiresAt).toISOString() : null
  };
}

function formatDateTime(value?: string | null): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '-';
  }

  return date.toLocaleString();
}

export function AnnouncementsPage() {
  const message = useMessage();
  const confirm = useConfirm();
  const { translate } = useI18n();

  const statusOptions: Array<{ label: string; value: AnnouncementEffectiveStatus | '' }> = useMemo(
    () => [
      { label: translate('page.systemAnnouncements.status.all'), value: '' },
      { label: translate('page.systemAnnouncements.status.draft'), value: 'Draft' },
      { label: translate('page.systemAnnouncements.status.published'), value: 'Published' },
      { label: translate('page.systemAnnouncements.status.withdrawn'), value: 'Withdrawn' },
      { label: translate('page.systemAnnouncements.status.expired'), value: 'Expired' }
    ],
    [translate]
  );

  const statusLabels: Record<AnnouncementEffectiveStatus, string> = useMemo(
    () => ({
      Draft: translate('page.systemAnnouncements.status.draft'),
      Published: translate('page.systemAnnouncements.status.published'),
      Withdrawn: translate('page.systemAnnouncements.status.withdrawn'),
      Expired: translate('page.systemAnnouncements.status.expired')
    }),
    [translate]
  );

  const resourceApi = useMemo(
    () => ({
      list: systemAnnouncementApi.list,
      create: (request: AnnouncementFormState) => systemAnnouncementApi.create(toApiRequest(request)),
      update: (id: string, request: AnnouncementFormState) => systemAnnouncementApi.update(id, toApiRequest(request)),
      delete: systemAnnouncementApi.delete
    }),
    []
  );

  const resource = useCrudResource<SystemAnnouncementListItemDto, AnnouncementFormState, AnnouncementFormState, AnnouncementSearchState>({
    api: resourceApi,
    defaultFormState: {
      title: '',
      content: '',
      expiresAt: '',
      announcementType: 'General',
      scope: 'System',
      priority: 0,
      isPinned: false,
      remark: ''
    },
      defaultSearchState: {
        keyword: '',
        status: ''
      },
      getId: (item) => item.id,
      itemName: translate('page.systemAnnouncements.itemName'),
      queryKeyPrefix: ['system-announcements']
    });

  const publishMutation = useApiMutation({
    mutationFn: systemAnnouncementApi.publish,
    onSuccess: async () => {
      message.success(translate('page.systemAnnouncements.publishSuccess'));
      await resource.refresh();
    },
    onError: (error) => {
      message.error(getErrorMessage(error, translate('page.systemAnnouncements.publishFailed')));
    }
  });

  const withdrawMutation = useApiMutation({
    mutationFn: systemAnnouncementApi.withdraw,
    onSuccess: async () => {
      message.success(translate('page.systemAnnouncements.withdrawSuccess'));
      await resource.refresh();
    },
    onError: (error) => {
      message.error(getErrorMessage(error, translate('page.systemAnnouncements.withdrawFailed')));
    }
  });

  const topMutation = useApiMutation({
    mutationFn: (variables: { id: string; isTop: boolean }) => systemAnnouncementApi.setTop(variables.id, variables.isTop),
    onSuccess: async (_data, variables) => {
      message.success(variables.isTop ? translate('page.systemAnnouncements.topSuccess') : translate('page.systemAnnouncements.unpinSuccess'));
      await resource.refresh();
    },
    onError: (error) => {
      message.error(getErrorMessage(error, translate('page.systemAnnouncements.topFailed')));
    }
  });

  const columns: DataTableColumn<SystemAnnouncementListItemDto>[] = useMemo(
    () => [
      { key: 'rowIndex', title: translate('page.systemAnnouncements.columns.index'), width: '70px', align: 'center', responsivePriority: 100, render: (_row, index) => (resource.pageIndex - 1) * resource.pageSize + index + 1 },
      {
        key: 'title',
        title: translate('page.systemAnnouncements.columns.title'),
        responsivePriority: 100,
        render: (row) => (
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              {row.isPinned ? <span className="rounded bg-amber-100 px-1.5 py-0.5 text-xs text-amber-700">{translate('page.systemAnnouncements.pinned')}</span> : null}
              <span className="truncate font-medium text-gray-900">{row.title}</span>
            </div>
            <div className="mt-1 line-clamp-1 text-xs text-gray-500">{row.content}</div>
          </div>
        )
      },
      {
        key: 'effectiveStatus',
        title: translate('page.systemAnnouncements.columns.status'),
        width: '100px',
        align: 'center',
        sortable: false,
        render: (row) => statusLabels[row.effectiveStatus]
      },
      {
        key: 'expiresAt',
        title: translate('page.systemAnnouncements.columns.expiresAt'),
        width: '180px',
        responsivePriority: 80,
        render: (row) => formatDateTime(row.expiresAt)
      },
      {
        key: 'publishedAt',
        title: translate('page.systemAnnouncements.columns.publishedAt'),
        width: '180px',
        responsivePriority: 70,
        render: (row) => formatDateTime(row.publishedAt)
      }
    ],
    [resource.pageIndex, resource.pageSize, statusLabels, translate]
  );

  const formFields: FormFieldConfig<AnnouncementFormState>[] = useMemo(
    () => [
      { label: translate('page.systemAnnouncements.form.title'), name: 'title', placeholder: translate('page.systemAnnouncements.form.titlePlaceholder'), required: true, span: 2, type: 'text', section: translate('page.systemAnnouncements.section.content') },
      { label: translate('page.systemAnnouncements.form.content'), name: 'content', placeholder: translate('page.systemAnnouncements.form.contentPlaceholder'), required: true, rows: 8, span: 2, type: 'textarea', section: translate('page.systemAnnouncements.section.content') },
      { label: translate('page.systemAnnouncements.form.expiresAt'), name: 'expiresAt', helpText: translate('page.systemAnnouncements.form.expiresAtHelp'), span: 2, type: 'datetime-local', section: translate('page.systemAnnouncements.section.publishSettings') }
    ],
    [translate]
  );

  const handlePublish = (row: SystemAnnouncementListItemDto) => {
    confirm({
      title: translate('page.systemAnnouncements.confirm.publishTitle'),
      content: formatMessage(translate('page.systemAnnouncements.confirm.publishContent'), { title: row.title }),
      onConfirm: async () => {
        await publishMutation.mutateAsync(row.id);
      }
    });
  };

  const handleWithdraw = (row: SystemAnnouncementListItemDto) => {
    confirm({
      title: translate('page.systemAnnouncements.confirm.withdrawTitle'),
      content: formatMessage(translate('page.systemAnnouncements.confirm.withdrawContent'), { title: row.title }),
      onConfirm: async () => {
        await withdrawMutation.mutateAsync(row.id);
      }
    });
  };

  const actionNode = (
    <div className="flex items-center gap-2">
      <button className="flex items-center gap-1 rounded border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700 shadow-sm transition-colors hover:bg-gray-50 hover:text-primary-600" type="button" onClick={() => void resource.refresh()}>
        <AppIcon name="arrows-clockwise" />{translate('page.systemAnnouncements.refresh')}
      </button>
      <PermissionButton code="system:announcement:add" className="flex items-center gap-1 rounded bg-primary-600 px-3 py-1.5 text-sm font-medium text-white shadow-sm transition-colors hover:bg-primary-700" type="button" onClick={resource.handleCreate}>
        <AppIcon name="plus" />{formatMessage(translate('platform.actions.create'), { itemName: translate('page.systemAnnouncements.itemName') })}
      </PermissionButton>
    </div>
  );

  const searchNode = (
    <SearchForm
      fields={[
        { label: translate('page.systemAnnouncements.keyword'), name: 'keyword', placeholder: translate('page.systemAnnouncements.keywordPlaceholder'), type: 'text' },
        {
          emptyOptionLabel: translate('page.systemAnnouncements.status.all'),
          label: translate('page.systemAnnouncements.statusLabel'),
          name: 'status',
          options: statusOptions.filter((option) => option.value).map((option) => ({ label: option.label, value: option.value })),
          type: 'select'
        }
      ]}
      onReset={resource.handleReset}
      onSubmit={(value) => resource.handleSearch(value)}
      onValueChange={(value) => resource.setSearchDraft(value)}
      value={resource.searchDraft}
    />
  );

  return (
    <CrudPage title={translate('page.systemAnnouncements.title')} description={translate('page.systemAnnouncements.description')} actions={actionNode} searchArea={searchNode}>
      <div className="flex h-full flex-1 gap-3 overflow-hidden">
        <div className="flex h-full min-w-0 flex-1 flex-col">
          <DataTable
            columnSettingsKey="system-announcements"
            columns={columns}
            emptyText={resource.listQuery.isError ? translate('page.systemAnnouncements.loadFailed') : translate('page.systemAnnouncements.empty')}
            fitScreen
            loading={resource.listQuery.isLoading}
            onPageChange={resource.setPageIndex}
            onPageSizeChange={resource.setPageSize}
            onQueryChange={resource.setTableQuery}
            onSortsChange={resource.setSorts}
            pageSizeOptions={[10, 20, 50]}
            pagination={{
              current: resource.pageIndex,
              pageSize: resource.pageSize,
              total: resource.listQuery.data?.data?.total ?? 0
            }}
            rowActions={(row) => (
              <TableActions>
                <PermissionButton
                  className="transition-colors hover:text-primary-600"
                  code="system:announcement:edit"
                  title={translate('platform.actions.edit')}
                  type="button"
                  onClick={() =>
                    resource.handleEdit(row, (item) => ({
                      title: item.title,
                      content: item.content,
                      expiresAt: toLocalDateTimeInput(item.expiresAt),
                      announcementType: item.announcementType || 'General',
                      scope: item.scope || 'System',
                      priority: item.priority ?? 0,
                      isPinned: item.isPinned,
                      remark: item.remark ?? ''
                    }))
                  }
                >
                  <AppIcon className="text-base" name="pencil-simple" />
                </PermissionButton>
                {row.effectiveStatus !== 'Published' ? (
                  <PermissionButton className="transition-colors hover:text-emerald-600" code="system:announcement:publish" title={translate('page.systemAnnouncements.action.publish')} type="button" onClick={() => handlePublish(row)}>
                    <AppIcon className="text-base" name="paper-plane-tilt" />
                  </PermissionButton>
                ) : (
                  <PermissionButton className="transition-colors hover:text-orange-600" code="system:announcement:withdraw" title={translate('page.systemAnnouncements.action.withdraw')} type="button" onClick={() => handleWithdraw(row)}>
                    <AppIcon className="text-base" name="arrow-u-up-left" />
                  </PermissionButton>
                )}
                <PermissionButton
                  className="transition-colors hover:text-amber-600 disabled:cursor-not-allowed disabled:text-gray-300"
                  code="system:announcement:top"
                  disabled={row.effectiveStatus !== 'Published'}
                  title={row.isPinned ? translate('page.systemAnnouncements.action.unpin') : translate('page.systemAnnouncements.action.pin')}
                  type="button"
                  onClick={() => topMutation.mutateAsync({ id: row.id, isTop: !row.isPinned })}
                >
                  <AppIcon className="text-base" name={row.isPinned ? 'push-pin-slash' : 'push-pin'} />
                </PermissionButton>
                <PermissionButton className="transition-colors hover:text-red-600" code="system:announcement:delete" title={translate('page.systemAnnouncements.action.delete')} type="button" onClick={() => resource.handleDelete(row, row.title)}>
                  <AppIcon className="text-base" name="trash" />
                </PermissionButton>
              </TableActions>
            )}
            rowKey={(row) => row.id}
            rows={resource.listQuery.data?.data.items ?? []}
            sorts={resource.sorts}
            tableQuery={resource.tableQuery}
          />
        </div>
      </div>

      <ModalForm
        actions={[
          { label: translate('common.cancel'), onClick: () => resource.setModalOpen(false), variant: 'ghost' },
          { label: translate('common.save'), onClick: () => void resource.handleSubmit(), type: 'button', variant: 'primary', loading: resource.createMutation.isPending || resource.updateMutation.isPending }
        ]}
        fields={formFields}
        open={resource.modalOpen}
        onClose={() => resource.setModalOpen(false)}
        onValueChange={(name, value) =>
          resource.setFormState((currentValue) => ({
            ...currentValue,
            [name]: value
          }))
        }
        title={resource.editingId ? formatMessage(translate('platform.modal.edit'), { itemName: translate('page.systemAnnouncements.itemName') }) : formatMessage(translate('platform.modal.create'), { itemName: translate('page.systemAnnouncements.itemName') })}
        value={resource.formState}
      />
    </CrudPage>
  );
}
