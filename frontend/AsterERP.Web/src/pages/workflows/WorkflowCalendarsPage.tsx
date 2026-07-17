import { useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';

import type { WorkflowWorkCalendarDto, WorkflowWorkCalendarUpsertRequest } from '../../api/workflow/workflows.api';
import { deleteWorkflowCalendar, getWorkflowCalendars, saveWorkflowCalendar } from '../../api/workflow/workflows.api';
import { formatMessage } from '../../core/i18n/formatMessage';
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

const defaultCalendar: WorkflowWorkCalendarUpsertRequest = {
  appCode: null,
  calendarDate: '',
  calendarName: '',
  dayType: 'Workday',
  isWorkingDay: true,
  remark: '',
  tenantId: null
};

export function WorkflowCalendarsPage() {
  const { translate } = useI18n();
  const workspace = useWorkspaceStore((state) => state.currentWorkspace);
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [keyword, setKeyword] = useState('');
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [modalOpen, setModalOpen] = useState(false);
  const [formState, setFormState] = useState<WorkflowWorkCalendarUpsertRequest>(defaultCalendar);

  const calendarsQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) => getWorkflowCalendars({ appCode: workspace?.appCode, keyword, pageIndex, pageSize, tenantId: workspace?.tenantId }, signal),
    queryKey: ['workflows', 'calendars', workspace?.tenantId, workspace?.appCode, keyword, pageIndex, pageSize]
  });
  const saveMutation = useApiMutation({ mutationFn: saveWorkflowCalendar });
  const deleteMutation = useApiMutation({ mutationFn: deleteWorkflowCalendar });

  const fields = useMemo<FormFieldConfig<WorkflowWorkCalendarUpsertRequest>[]>(() => [
    { label: translate('workflow.calendars.date'), name: 'calendarDate', required: true, type: 'date' },
    { label: translate('workflow.calendars.name'), name: 'calendarName', required: true, type: 'text' },
    { label: translate('workflow.calendars.type'), name: 'dayType', options: [{ label: translate('workflow.calendars.typeWorkday'), value: 'Workday' }, { label: translate('workflow.calendars.typeHoliday'), value: 'Holiday' }, { label: translate('workflow.calendars.typeAdjustedWorkday'), value: 'AdjustedWorkday' }], required: true, type: 'select' },
    { label: translate('workflow.calendars.workingDay'), name: 'isWorkingDay', type: 'switch' },
    { label: translate('workflow.calendars.remark'), name: 'remark', rows: 4, type: 'textarea' }
  ], [translate]);

  const columns = useMemo<DataTableColumn<WorkflowWorkCalendarDto>[]>(() => [
    { key: 'calendarDate', title: translate('workflow.calendars.table.date'), width: '150px', responsivePriority: 100, render: (row) => formatDate(row.calendarDate) },
    { key: 'calendarName', title: translate('workflow.calendars.table.name'), width: '220px', responsivePriority: 90 },
    { key: 'dayType', title: translate('workflow.calendars.table.type'), width: '130px', render: (row) => dayTypeLabel(row.dayType, translate) },
    { key: 'isWorkingDay', title: translate('workflow.calendars.table.workingDay'), width: '100px', render: (row) => row.isWorkingDay ? translate('workflow.calendars.statusYes') : translate('workflow.calendars.statusNo') },
    { key: 'remark', title: translate('workflow.calendars.table.remark'), width: '240px', hideBelow: 'lg', render: (row) => row.remark || '-' }
  ], [translate]);

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['workflows', 'calendars'] });

  const openCreate = () => {
    setFormState({ ...defaultCalendar, appCode: workspace?.appCode ?? 'SYSTEM', tenantId: workspace?.tenantId ?? 'tenant-system' });
    setModalOpen(true);
  };

  const openEdit = (row: WorkflowWorkCalendarDto) => {
    setFormState({
      appCode: row.appCode,
      calendarDate: row.calendarDate.slice(0, 10),
      calendarName: row.calendarName,
      dayType: row.dayType,
      id: row.id,
      isWorkingDay: row.isWorkingDay,
      remark: row.remark ?? '',
      tenantId: row.tenantId
    });
    setModalOpen(true);
  };

  const save = async () => {
    try {
      await saveMutation.mutateAsync(formState);
      setModalOpen(false);
      await refresh();
      message.success(translate('workflow.calendars.saveSuccess'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('workflow.calendars.saveFailed')));
    }
  };

  const remove = (row: WorkflowWorkCalendarDto) => {
    confirm({
      title: translate('workflow.calendars.confirmDeleteTitle'),
      content: formatMessage(translate('workflow.calendars.confirmDeleteContent'), { date: formatDate(row.calendarDate) }),
      confirmText: translate('workflow.calendars.confirmDeleteConfirm'),
      onConfirm: async () => {
        await deleteMutation.mutateAsync(row.id);
        await refresh();
        message.success(translate('workflow.calendars.deleteSuccess'));
      }
    });
  };

  return (
    <CrudPage
      title={translate('workflow.calendars.title')}
      description={translate('workflow.calendars.description')}
      actions={(
        <div className="workflow-page-actions">
          <label className="workflow-toolbar-search">
            <AppIcon name="magnifying-glass" />
            <input placeholder={translate('workflow.calendars.searchPlaceholder')} value={keyword} onChange={(event) => { setKeyword(event.target.value); setPageIndex(1); }} />
          </label>
          <PermissionButton code="workflow:calendar:edit" className="primary-button" type="button" onClick={openCreate}><AppIcon name="plus" />{translate('workflow.calendars.create')}</PermissionButton>
        </div>
      )}
      editor={(
        <ModalForm
          actions={[
            { label: translate('workflow.drawer.cancel'), onClick: () => setModalOpen(false) },
            { label: translate('workflow.drawer.submit'), loading: saveMutation.isPending, onClick: () => void save(), variant: 'primary' }
          ]}
          fields={fields}
          open={modalOpen}
          title={formState.id ? translate('workflow.calendars.modalEdit') : translate('workflow.calendars.modalCreate')}
          value={formState}
          onClose={() => setModalOpen(false)}
          onValueChange={(name, value) => setFormState((current) => ({ ...current, [name]: value }))}
        />
      )}
    >
      <div className="flex-1 min-h-0 rounded-lg border border-gray-200 bg-white shadow-sm">
        <DataTable
          columnSettingsKey="workflow-calendars"
          columns={columns}
          emptyText={calendarsQuery.isError ? translate('workflow.calendars.loadFailed') : translate('workflow.calendars.empty')}
          fitScreen
          loading={calendarsQuery.isLoading}
          onPageChange={setPageIndex}
          onPageSizeChange={(next) => { setPageSize(next); setPageIndex(1); }}
          pagination={{ current: pageIndex, pageSize, total: calendarsQuery.data?.data.total ?? 0 }}
          rowActions={(row) => (
            <TableActions>
              <PermissionButton code="workflow:calendar:edit" className="hover:text-primary-600" title={translate('workflow.calendars.edit')} type="button" onClick={() => openEdit(row)}><AppIcon className="text-base" name="pencil-simple" /></PermissionButton>
              <PermissionButton code="workflow:calendar:delete" className="hover:text-red-600" title={translate('workflow.calendars.delete')} type="button" onClick={() => remove(row)}><AppIcon className="text-base" name="trash" /></PermissionButton>
            </TableActions>
          )}
          rowKey={(row) => row.id}
          rows={calendarsQuery.data?.data.items ?? []}
        />
      </div>
    </CrudPage>
  );
}

function formatDate(value?: string | null) {
  return value ? new Date(value).toLocaleDateString() : '-';
}

function dayTypeLabel(value: string, translate: (key: string) => string) {
  if (value === 'Holiday') return translate('workflow.calendars.typeHoliday');
  if (value === 'AdjustedWorkday') return translate('workflow.calendars.typeAdjustedWorkday');
  return translate('workflow.calendars.typeWorkday');
}
