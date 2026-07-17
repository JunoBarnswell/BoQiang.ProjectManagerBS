import { useQueryClient } from '@tanstack/react-query';
import { useMemo, useRef, useState, type ChangeEvent } from 'react';
import { useNavigate } from 'react-router-dom';

import { systemFilesApi } from '../../../api/system/files.api';
import type { SystemFileRecordDto } from '../../../api/system/files.types';
import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { queryKeys } from '../../../core/query/queryKeys';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { usePrintLauncher } from '../../../features/print-center/hooks/usePrintLauncher';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { CrudPage } from '../../../shared/components/crud-page/CrudPage';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import { FilePreviewDialog } from '../../../shared/file-preview/FilePreviewDialog';
import {
  createPreviewFile,
  formatFileSize,
  getFileExtension,
  getPreviewStatusLabel,
  saveBlob
} from '../../../shared/file-preview/filePreviewUtils';
import { SearchForm } from '../../../shared/forms/SearchForm';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { DataTable } from '../../../shared/table/DataTable';
import { TableActions } from '../../../shared/table/TableActions';
import type { DataTableColumn, DataTableQueryState, DataTableSortRule } from '../../../shared/table/tableTypes';
import { useTabPageState } from '../../../shared/tabs/useTabPageState';
import { getErrorMessage } from '../../../shared/utils/errorMessage';

interface FileSearchState {
  keyword: string;
}

interface FilePageState {
  pageIndex: number;
  pageSize: number;
  search: FileSearchState;
  searchDraft: FileSearchState;
  sorts: DataTableSortRule[];
  tableQuery: DataTableQueryState;
}

interface PreviewState {
  error: string | null;
  file: SystemFileRecordDto | null;
  loading: boolean;
  open: boolean;
  previewFile: File | null;
}

const defaultSearchState: FileSearchState = { keyword: '' };
const defaultTableQuery: DataTableQueryState = { conditions: [], matchMode: 'and' };
const defaultPreviewState: PreviewState = {
  error: null,
  file: null,
  loading: false,
  open: false,
  previewFile: null
};

function formatDateTime(value: string): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function renderPreviewStatus(row: SystemFileRecordDto) {
  const className = row.previewSupported
    ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
    : 'border-gray-200 bg-gray-50 text-gray-500';

  return (
    <span className={`inline-flex items-center rounded border px-2 py-0.5 text-xs font-medium ${className}`}>
      {getPreviewStatusLabel(row)}
    </span>
  );
}

export function FilesPage() {
  const { translate } = useI18n();
  const navigate = useNavigate();
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const printLauncher = usePrintLauncher();
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [previewState, setPreviewState] = useState<PreviewState>(defaultPreviewState);

  const [pageState, setPageState, clearPageState] = useTabPageState<FilePageState>(
    {
      pageIndex: 1,
      pageSize: 20,
      search: defaultSearchState,
      searchDraft: defaultSearchState,
      sorts: [],
      tableQuery: defaultTableQuery
    },
    { cacheKey: 'system-files' }
  );

  const { pageIndex, pageSize, search, searchDraft, sorts, tableQuery } = pageState;

  const listQuery = useApiQuery({
    keepPreviousData: true,
    queryKey: queryKeys.systemFiles.list(pageIndex, pageSize, search.keyword, sorts, tableQuery),
    queryFn: ({ signal }) =>
      systemFilesApi.list({
        filters: tableQuery.conditions,
        keyword: search.keyword,
        pageIndex,
        pageSize,
        sorts
      }, signal)
  });

  const formatsQuery = useApiQuery({
    queryKey: queryKeys.systemFiles.formats(),
    queryFn: ({ signal }) => systemFilesApi.formats(signal),
    staleTimeMs: 10 * 60_000
  });

  const uploadMutation = useApiMutation({
    mutationFn: (file: File) => systemFilesApi.upload(file),
    onSuccess: async () => {
      message.success(translate('page.systemFiles.uploadSuccess'));
      await queryClient.invalidateQueries({ queryKey: queryKeys.systemFiles.root() });
    },
    onError: (error) => {
      message.error(getErrorMessage(error, translate('page.systemFiles.uploadFailed')));
    }
  });

  const deleteMutation = useApiMutation({
    mutationFn: (id: string) => systemFilesApi.delete(id),
    onSuccess: async () => {
      message.success(translate('crud.deleteSuccess').replace('{itemName}', translate('nav.systemFiles')));
      await queryClient.invalidateQueries({ queryKey: queryKeys.systemFiles.root() });
    },
    onError: (error) => {
      message.error(getErrorMessage(error, translate('crud.deleteFailed').replace('{itemName}', translate('nav.systemFiles'))));
    }
  });

  const columns: DataTableColumn<SystemFileRecordDto>[] = useMemo(
    () => [
      {
        key: 'rowIndex',
        title: translate('page.systemFiles.index'),
        width: '70px',
        align: 'center',
        responsivePriority: 100,
        render: (_row, index) => (pageIndex - 1) * pageSize + index + 1
      },
      {
        key: 'fileName',
        title: translate('page.systemFiles.fileName'),
        minWidth: '260px',
        responsivePriority: 100,
        sortable: true,
        filterable: true,
        filterType: 'text',
        render: (row) => (
          <div className="min-w-0">
            <div className="truncate font-medium text-gray-900" title={row.fileName}>{row.fileName}</div>
            <div className="mt-1 flex items-center gap-2 text-xs text-gray-500">
              <span className="uppercase">{row.extension || getFileExtension(row.fileName) || '-'}</span>
              <span>{row.contentType || 'application/octet-stream'}</span>
            </div>
          </div>
        )
      },
      {
        key: 'size',
        title: translate('page.systemFiles.size'),
        width: '110px',
        align: 'right',
        responsivePriority: 90,
        sortable: true,
        render: (row) => formatFileSize(row.size)
      },
      {
        key: 'previewSupported',
        title: translate('page.systemFiles.previewStatus'),
        width: '150px',
        align: 'center',
        responsivePriority: 95,
        render: renderPreviewStatus
      },
      {
        key: 'createdTime',
        title: translate('page.systemFiles.createdTime'),
        width: '190px',
        responsivePriority: 85,
        sortable: true,
        filterable: true,
        filterType: 'date',
        render: (row) => formatDateTime(row.createdTime)
      },
      {
        key: 'remark',
        title: translate('page.systemFiles.remark'),
        minWidth: '160px',
        responsivePriority: 60,
        filterable: true,
        filterType: 'text',
        render: (row) => row.remark || '-'
      }
    ],
    [pageIndex, pageSize, translate]
  );

  const handleSearch = (value: FileSearchState) => {
    setPageState((current) => ({
      ...current,
      pageIndex: 1,
      search: value,
      searchDraft: value
    }));
  };

  const handleReset = () => {
    clearPageState();
  };

  const handleSortsChange = (nextSorts: DataTableSortRule[]) => {
    setPageState((current) => ({ ...current, pageIndex: 1, sorts: nextSorts }));
  };

  const handleTableQueryChange = (nextQuery: DataTableQueryState) => {
    setPageState((current) => ({ ...current, pageIndex: 1, tableQuery: nextQuery }));
  };

  const handleFileInputChange = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) {
      return;
    }

    try {
      await uploadMutation.mutateAsync(file);
    } catch {
      // The mutation onError already surfaces the message through shared feedback.
    }
  };

  const handlePreview = async (row: SystemFileRecordDto) => {
    if (!row.previewSupported) {
      message.info(translate('page.systemFiles.previewUnavailable'));
      return;
    }

    setPreviewState({
      error: null,
      file: row,
      loading: true,
      open: true,
      previewFile: null
    });

    try {
      const response = await systemFilesApi.previewBlob(row);
      setPreviewState({
        error: null,
        file: row,
        loading: false,
        open: true,
        previewFile: createPreviewFile(row, response.blob, response.fileName)
      });
    } catch (error) {
      setPreviewState({
        error: getErrorMessage(error, translate('page.systemFiles.loadFailed')),
        file: row,
        loading: false,
        open: true,
        previewFile: null
      });
    }
  };

  const handleDownload = async (row: SystemFileRecordDto) => {
    try {
      const response = await systemFilesApi.downloadBlob(row);
      saveBlob(response.blob, response.fileName || row.fileName);
      message.success(translate('page.systemFiles.downloadSuccess'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemFiles.downloadFailed')));
    }
  };

  const handleDelete = (row: SystemFileRecordDto) => {
    confirm({
      title: translate('page.systemFiles.deleteTitle'),
      content: formatMessage(translate('page.systemFiles.deleteContent'), { name: row.fileName }),
      onConfirm: async () => {
        await deleteMutation.mutateAsync(row.id);
      }
    });
  };

  const searchNode = (
    <SearchForm
      fields={[
        { label: translate('page.systemFiles.keyword'), name: 'keyword', placeholder: translate('page.systemFiles.keywordPlaceholder'), type: 'text' }
      ]}
      onReset={handleReset}
      onSubmit={handleSearch}
      onValueChange={(value) => setPageState((current) => ({ ...current, searchDraft: value }))}
      value={searchDraft}
    />
  );

  const actionNode = (
    <div className="flex items-center gap-2">
      <button
        className="flex items-center gap-1 rounded border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700 shadow-sm transition-colors hover:bg-gray-50 hover:text-primary-600"
        type="button"
        onClick={() => void listQuery.refetch()}
      >
        <AppIcon name="arrows-clockwise" />{translate('page.systemFiles.refresh')}
      </button>
      <PermissionButton
        className="flex items-center gap-1 rounded border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700 shadow-sm transition-colors hover:bg-gray-50 hover:text-primary-600"
        code="system:print:use"
        type="button"
        onClick={() => printLauncher.open({
          conditions: tableQuery.conditions.map((item) => ({
            field: item.field,
            operator: item.operator,
            value: item.value,
            valueTo: item.valueTo
          })),
          menuCode: 'system:file',
          pageIndex,
          pageSize,
          scene: 'list',
          selectedIds: [],
          sorts: sorts.map((item) => ({ direction: item.direction, field: item.field }))
        })}
      >
        <AppIcon name="printer" />{translate('page.systemFiles.printList')}
      </PermissionButton>
      <PermissionButton
        className="flex items-center gap-1 rounded border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700 shadow-sm transition-colors hover:bg-gray-50 hover:text-primary-600"
        code="system:print:edit"
        type="button"
        onClick={() => navigate('/system/print-center/new?menuCode=system:file&scene=list&returnTo=/system/files')}
      >
        <AppIcon name="gear-six" />{translate('page.systemFiles.configureListTemplate')}
      </PermissionButton>
      <PermissionButton
        className="flex items-center gap-1 rounded bg-primary-600 px-3 py-1.5 text-sm font-medium text-white shadow-sm transition-colors hover:bg-primary-700 disabled:cursor-not-allowed disabled:bg-gray-300"
        code="system:file:upload"
        disabled={uploadMutation.isPending}
        iconStart={false}
        type="button"
        onClick={() => fileInputRef.current?.click()}
      >
        <AppIcon name="upload-simple" />{translate('page.systemFiles.upload')}
      </PermissionButton>
      <input ref={fileInputRef} className="hidden" type="file" onChange={(event) => void handleFileInputChange(event)} />
    </div>
  );

  return (
    <CrudPage
      actions={actionNode}
      description={`${translate('page.systemFiles.description')} ${formatMessage(translate('page.systemFiles.extensionsSummary'), { count: formatsQuery.data?.data?.length ?? '-' })}`}
      eyebrow={translate('page.systemFiles.eyebrow')}
      searchArea={searchNode}
      title={translate('page.systemFiles.title')}
    >
      <div className="flex h-full min-w-0 flex-1 flex-col overflow-hidden">
        <DataTable
          columnSettingsKey="system-files"
          columns={columns}
          emptyText={listQuery.isError ? translate('page.systemFiles.loadFailed') : translate('page.systemFiles.empty')}
          fitScreen
          loading={listQuery.isLoading}
          onPageChange={(nextPage) => setPageState((current) => ({ ...current, pageIndex: nextPage }))}
          onPageSizeChange={(nextPageSize) => setPageState((current) => ({ ...current, pageIndex: 1, pageSize: nextPageSize }))}
          onQueryChange={handleTableQueryChange}
          onSortsChange={handleSortsChange}
          pageSizeOptions={[10, 20, 50, 100]}
          pagination={{
            current: pageIndex,
            pageSize,
            total: listQuery.data?.data.total ?? 0
          }}
          rowActions={(row) => (
            <TableActions>
              <PermissionButton
                className="transition-colors hover:text-primary-600 disabled:cursor-not-allowed disabled:text-gray-300"
                code="system:file:preview"
                disabled={!row.previewSupported}
                title={row.previewSupported ? translate('page.systemFiles.preview') : translate('page.systemFiles.previewUnsupported')}
                type="button"
                onClick={() => void handlePreview(row)}
              >
                <AppIcon className="text-base" name="eye" />
              </PermissionButton>
              <PermissionButton className="transition-colors hover:text-primary-600" code="system:file:download" title={translate('page.systemFiles.download')} type="button" onClick={() => void handleDownload(row)}>
                <AppIcon className="text-base" name="download-simple" />
              </PermissionButton>
              <PermissionButton className="transition-colors hover:text-primary-600" code="system:print:use" title={translate('page.systemFiles.printDetail')} type="button" onClick={() => printLauncher.open({
                conditions: [],
                detailId: row.id,
                menuCode: 'system:file',
                pageIndex: 1,
                pageSize: 1,
                scene: 'detail',
                selectedIds: [],
                sorts: []
              })}>
                <AppIcon className="text-base" name="printer" />
              </PermissionButton>
              <PermissionButton className="transition-colors hover:text-primary-600" code="system:print:edit" title={translate('page.systemFiles.configureDetailTemplate')} type="button" onClick={() => navigate('/system/print-center/new?menuCode=system:file&scene=detail&returnTo=/system/files')}>
                <AppIcon className="text-base" name="gear-six" />
              </PermissionButton>
              <PermissionButton className="transition-colors hover:text-red-600" code="system:file:delete" iconStart={false} title={translate('page.systemFiles.delete')} type="button" onClick={() => handleDelete(row)}>
                <AppIcon className="text-base" name="trash" />
              </PermissionButton>
            </TableActions>
          )}
          rowKey={(row) => row.id}
          rows={listQuery.data?.data.items ?? []}
          sorts={sorts}
          tableQuery={tableQuery}
        />
      </div>

      <FilePreviewDialog
        error={previewState.error}
        file={previewState.file}
        loading={previewState.loading}
        open={previewState.open}
        previewFile={previewState.previewFile}
        onClose={() => setPreviewState(defaultPreviewState)}
      />

      {printLauncher.dialog}
    </CrudPage>
  );
}

export default FilesPage;
