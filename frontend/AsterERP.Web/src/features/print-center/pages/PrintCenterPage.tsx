import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { CrudPage } from '../../../shared/components/crud-page/CrudPage';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import { SearchForm } from '../../../shared/forms/SearchForm';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { DataTable } from '../../../shared/table/DataTable';
import { TableActions } from '../../../shared/table/TableActions';
import type { DataTableColumn } from '../../../shared/table/tableTypes';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { printCenterApi } from '../api/printCenter.api';
import type { PrintTargetOptionDto, PrintTemplateListItemDto } from '../types';

interface PrintCenterSearchState {
  keyword: string;
  menuCode: string;
  scene: string;
  status: string;
}

const defaultSearchState: PrintCenterSearchState = {
  keyword: '',
  menuCode: '',
  scene: '',
  status: ''
};

function formatDateTime(value: number): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '-' : date.toLocaleString();
}

export function PrintCenterPage() {
  const { translate } = useI18n();
  const navigate = useNavigate();
  const message = useMessage();
  const confirm = useConfirm();
  const [search, setSearch] = useState<PrintCenterSearchState>(defaultSearchState);

  const targetsQuery = useApiQuery({
    queryFn: ({ signal }) => printCenterApi.getTargets(signal),
    queryKey: ['print-center', 'targets']
  });

  const templatesQuery = useApiQuery({
    queryFn: ({ signal }) => printCenterApi.getTemplates(search, signal),
    queryKey: ['print-center', 'templates', search.keyword, search.menuCode, search.scene, search.status]
  });

  const publishMutation = useApiMutation({
    mutationFn: (id: string) => printCenterApi.publishTemplate(id)
  });
  const setDefaultMutation = useApiMutation({
    mutationFn: (id: string) => printCenterApi.setDefaultTemplate(id)
  });
  const deleteMutation = useApiMutation({
    mutationFn: (id: string) => printCenterApi.deleteTemplate(id)
  });

  const targetOptions = useMemo(
    () => (targetsQuery.data?.data ?? []).map((item: PrintTargetOptionDto) => ({ label: item.menuName, value: item.menuCode })),
    [targetsQuery.data?.data]
  );

  const columns: DataTableColumn<PrintTemplateListItemDto>[] = useMemo(
    () => [
      { key: 'name', title: translate('print.columns.name'), width: '220px', responsivePriority: 100, render: (row) => row.name },
      { key: 'menuName', title: translate('print.columns.menuName'), width: '180px', responsivePriority: 95, render: (row) => row.menuName },
      { key: 'scene', title: translate('print.columns.scene'), width: '90px', responsivePriority: 90, render: (row) => row.scene === 'list' ? translate('print.scene.list') : translate('print.scene.detail') },
      { key: 'templateCode', title: translate('print.columns.templateCode'), width: '180px', responsivePriority: 85, render: (row) => row.templateCode },
      { key: 'status', title: translate('print.columns.status'), width: '100px', responsivePriority: 80, render: (row) => row.status },
      { key: 'isDefault', title: translate('print.columns.isDefault'), width: '80px', align: 'center', responsivePriority: 78, render: (row) => row.isDefault ? translate('print.default.yes') : translate('print.default.no') },
      { key: 'updatedAt', title: translate('print.columns.updatedAt'), width: '180px', responsivePriority: 70, render: (row) => formatDateTime(row.updatedAt) }
    ],
    [translate]
  );

  const handlePublish = async (template: PrintTemplateListItemDto) => {
    try {
      await publishMutation.mutateAsync(template.id);
      await templatesQuery.refetch();
      message.success(translate('print.publishSuccess'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('print.publishFailed')));
    }
  };

  const handleSetDefault = async (template: PrintTemplateListItemDto) => {
    try {
      await setDefaultMutation.mutateAsync(template.id);
      await templatesQuery.refetch();
      message.success(translate('print.setDefaultSuccess'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('print.setDefaultFailed')));
    }
  };

  const handleDelete = (template: PrintTemplateListItemDto) => {
    confirm({
      title: translate('print.deleteTitle'),
      content: formatMessage(translate('print.deleteContent'), { name: template.name }),
      onConfirm: async () => {
        try {
          await deleteMutation.mutateAsync(template.id);
          await templatesQuery.refetch();
          message.success(translate('print.deleteSuccess'));
        } catch (error) {
          message.error(getErrorMessage(error, translate('print.deleteFailed')));
        }
      }
    });
  };

  const actionNode = (
    <div className="flex items-center gap-2">
      <button
        className="flex items-center gap-1 rounded border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50"
        type="button"
        onClick={() => void templatesQuery.refetch()}
      >
        <AppIcon name="arrows-clockwise" /> {translate('print.actions.refresh')}
      </button>
      <button
        className="flex items-center gap-1 rounded bg-primary-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-primary-700"
        type="button"
        onClick={() => navigate('/system/print-center/new')}
      >
        <AppIcon name="plus" /> {translate('print.actions.newTemplate')}
      </button>
    </div>
  );

  const searchNode = (
    <SearchForm
      fields={[
        { label: translate('print.search.keyword'), name: 'keyword', placeholder: translate('print.search.keywordPlaceholder'), type: 'text' },
        { emptyOptionLabel: translate('print.search.allMenus'), label: translate('print.search.menu'), name: 'menuCode', options: targetOptions, type: 'select' },
        {
          emptyOptionLabel: translate('print.search.allScenes'),
          label: translate('print.search.scene'),
          name: 'scene',
          options: [
            { label: translate('print.scene.list'), value: 'list' },
            { label: translate('print.scene.detail'), value: 'detail' }
          ],
          type: 'select'
        },
        {
          emptyOptionLabel: translate('print.search.allStatus'),
          label: translate('print.search.status'),
          name: 'status',
          options: [
            { label: translate('print.status.draft'), value: 'Draft' },
            { label: translate('print.status.published'), value: 'Published' }
          ],
          type: 'select'
        }
      ]}
      onReset={() => setSearch(defaultSearchState)}
      onSubmit={(value) => setSearch(value)}
      onValueChange={(value) => setSearch(value)}
      value={search}
    />
  );

  return (
    <CrudPage
      actions={actionNode}
      description={translate('print.description')}
      eyebrow={translate('print.eyebrow')}
      searchArea={searchNode}
      title={translate('print.title')}
    >
      <div className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-lg border border-gray-200 bg-white shadow-sm">
        <DataTable
          columnSettingsKey="system-print-center"
          columns={columns}
          emptyText={templatesQuery.isError ? translate('print.loadFailed') : translate('print.empty')}
          fitScreen
          loading={templatesQuery.isLoading}
          rowActions={(row) => (
            <TableActions>
              <button className="transition-colors hover:text-primary-600" title={translate('print.actions.design')} type="button" onClick={() => navigate(`/system/print-center/${row.id}/designer`)}>
                <AppIcon className="text-base" name="pencil-simple" />
              </button>
              <button className="transition-colors hover:text-primary-600" title={translate('print.actions.publish')} type="button" onClick={() => void handlePublish(row)}>
                <AppIcon className="text-base" name="paper-plane-tilt" />
              </button>
              <button className="transition-colors hover:text-primary-600" title={translate('print.actions.setDefault')} type="button" onClick={() => void handleSetDefault(row)}>
                <AppIcon className="text-base" name="star" />
              </button>
              <button className="transition-colors hover:text-red-600" title={translate('print.actions.delete')} type="button" onClick={() => handleDelete(row)}>
                <AppIcon className="text-base" name="trash" />
              </button>
            </TableActions>
          )}
          rowKey={(row) => row.id}
          rows={templatesQuery.data?.data ?? []}
        />
      </div>
    </CrudPage>
  );
}
