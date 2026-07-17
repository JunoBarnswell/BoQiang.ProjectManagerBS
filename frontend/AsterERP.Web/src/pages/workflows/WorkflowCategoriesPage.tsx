import { useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';

import type { WorkflowCategoryDto, WorkflowCategoryUpsertRequest } from '../../api/workflow/workflows.api';
import { deleteWorkflowCategory, getWorkflowCategories, saveWorkflowCategory } from '../../api/workflow/workflows.api';
import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useApiQuery } from '../../core/query/useApiQuery';
import { useWorkspaceStore } from '../../core/state';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import { useConfirm } from '../../shared/feedback/useConfirm';
import { useMessage } from '../../shared/feedback/useMessage';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import { ModalForm } from '../../shared/forms/ModalForm';
import { AppIcon } from '../../shared/icons/AppIcon';
import { DataTable } from '../../shared/table/DataTable';
import { TableActions } from '../../shared/table/TableActions';
import type { DataTableColumn } from '../../shared/table/tableTypes';
import { getErrorMessage } from '../../shared/utils/errorMessage';

const defaultCategory: WorkflowCategoryUpsertRequest = {
  appCode: null,
  categoryCode: '',
  categoryName: '',
  isEnabled: true,
  parentCode: '',
  remark: '',
  sortOrder: 10,
  tenantId: null
};

export function WorkflowCategoriesPage() {
  const { translate } = useI18n();
  const workspace = useWorkspaceStore((state) => state.currentWorkspace);
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [keyword, setKeyword] = useState('');
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [modalOpen, setModalOpen] = useState(false);
  const [formState, setFormState] = useState<WorkflowCategoryUpsertRequest>(defaultCategory);

  const categoriesQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) => getWorkflowCategories({ appCode: workspace?.appCode, keyword, pageIndex, pageSize, tenantId: workspace?.tenantId }, signal),
    queryKey: ['workflows', 'categories', workspace?.tenantId, workspace?.appCode, keyword, pageIndex, pageSize]
  });
  const saveMutation = useApiMutation({ mutationFn: saveWorkflowCategory });
  const deleteMutation = useApiMutation({ mutationFn: deleteWorkflowCategory });

  const fields = useMemo<FormFieldConfig<WorkflowCategoryUpsertRequest>[]>(() => [
    { label: translate('page.workflowCategories.field.categoryCode'), name: 'categoryCode', required: true, type: 'text' },
    { label: translate('page.workflowCategories.field.categoryName'), name: 'categoryName', required: true, type: 'text' },
    { label: translate('page.workflowCategories.field.parentCode'), name: 'parentCode', type: 'text' },
    { label: translate('page.workflowCategories.field.sortOrder'), name: 'sortOrder', type: 'number' },
    { label: translate('page.workflowCategories.field.isEnabled'), name: 'isEnabled', type: 'switch' },
    { label: translate('page.workflowCategories.field.remark'), name: 'remark', rows: 4, type: 'textarea' }
  ], [translate]);

  const columns = useMemo<DataTableColumn<WorkflowCategoryDto>[]>(() => [
    { key: 'categoryName', title: translate('page.workflowCategories.column.category'), width: '220px', responsivePriority: 100, render: (row) => <><div className="font-medium text-gray-900">{row.categoryName}</div><div className="font-mono text-xs text-gray-500">{row.categoryCode}</div></> },
    { key: 'parentCode', title: translate('page.workflowCategories.column.parentCode'), width: '140px', hideBelow: 'lg', render: (row) => row.parentCode || '-' },
    { key: 'sortOrder', title: translate('page.workflowCategories.column.sortOrder'), width: '90px' },
    { key: 'isEnabled', title: translate('page.workflowCategories.column.status'), width: '100px', render: (row) => row.isEnabled ? translate('common.enabled') : translate('common.disabled') },
    { key: 'updatedAt', title: translate('page.workflowCategories.column.updatedAt'), width: '180px', hideBelow: 'lg', render: (row) => formatDateTime(row.updatedAt ?? row.createdAt) }
  ], [translate]);

  const openCreate = () => {
    setFormState({ ...defaultCategory, appCode: workspace?.appCode ?? 'SYSTEM', tenantId: workspace?.tenantId ?? 'tenant-system' });
    setModalOpen(true);
  };

  const openEdit = (row: WorkflowCategoryDto) => {
    setFormState({
      appCode: row.appCode,
      categoryCode: row.categoryCode,
      categoryName: row.categoryName,
      id: row.id,
      isEnabled: row.isEnabled,
      parentCode: row.parentCode ?? '',
      remark: row.remark ?? '',
      sortOrder: row.sortOrder,
      tenantId: row.tenantId
    });
    setModalOpen(true);
  };

  const save = async () => {
    try {
      await saveMutation.mutateAsync(formState);
      setModalOpen(false);
      await queryClient.invalidateQueries({ queryKey: ['workflows', 'categories'] });
      message.success(translate('page.workflowCategories.success.save'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.workflowCategories.error.saveFailed')));
    }
  };

  const remove = (row: WorkflowCategoryDto) => {
    confirm({
      title: translate('page.workflowCategories.confirm.deleteTitle'),
      content: translate('page.workflowCategories.confirm.deleteContent').replace('{name}', row.categoryName),
      confirmText: translate('page.workflowCategories.confirm.deleteAction'),
      onConfirm: async () => {
        await deleteMutation.mutateAsync(row.id);
        await queryClient.invalidateQueries({ queryKey: ['workflows', 'categories'] });
        message.success(translate('page.workflowCategories.success.delete'));
      }
    });
  };

  return (
    <CrudPage
      title={translate('page.workflowCategories.title')}
      description={translate('page.workflowCategories.description')}
      actions={(
        <div className="workflow-page-actions">
          <label className="workflow-toolbar-search">
            <AppIcon name="magnifying-glass" />
            <input placeholder={translate('page.workflowCategories.search.placeholder')} value={keyword} onChange={(event) => { setKeyword(event.target.value); setPageIndex(1); }} />
          </label>
          <PermissionButton code="workflow:category:edit" className="primary-button" type="button" onClick={openCreate}><AppIcon name="plus" />{translate('page.workflowCategories.action.create')}</PermissionButton>
        </div>
      )}
      editor={(
        <ModalForm
          actions={[
            { label: translate('common.cancel'), onClick: () => setModalOpen(false) },
            { label: translate('common.save'), loading: saveMutation.isPending, onClick: () => void save(), variant: 'primary' }
          ]}
          fields={fields}
          open={modalOpen}
          title={formState.id ? translate('page.workflowCategories.modal.editTitle') : translate('page.workflowCategories.modal.createTitle')}
          value={formState}
          onClose={() => setModalOpen(false)}
          onValueChange={(name, value) => setFormState((current) => ({ ...current, [name]: value }))}
        />
      )}
    >
      <div className="flex-1 min-h-0 rounded-lg border border-gray-200 bg-white shadow-sm">
        <DataTable
          columnSettingsKey="workflow-categories"
          columns={columns}
          emptyText={categoriesQuery.isError ? translate('page.workflowCategories.error.loadFailed') : translate('page.workflowCategories.empty')}
          fitScreen
          loading={categoriesQuery.isLoading}
          onPageChange={setPageIndex}
          onPageSizeChange={(next) => { setPageSize(next); setPageIndex(1); }}
          pagination={{ current: pageIndex, pageSize, total: categoriesQuery.data?.data.total ?? 0 }}
          rowActions={(row) => (
            <TableActions>
              <PermissionButton code="workflow:category:edit" className="hover:text-primary-600" title={translate('common.edit')} type="button" onClick={() => openEdit(row)}><AppIcon className="text-base" name="pencil-simple" /></PermissionButton>
              <PermissionButton code="workflow:category:delete" className="hover:text-red-600" title={translate('common.delete')} type="button" onClick={() => remove(row)}><AppIcon className="text-base" name="trash" /></PermissionButton>
            </TableActions>
          )}
          rowKey={(row) => row.id}
          rows={categoriesQuery.data?.data.items ?? []}
        />
      </div>
    </CrudPage>
  );
}

function formatDateTime(value?: string | null) {
  return value ? new Date(value).toLocaleString() : '-';
}
