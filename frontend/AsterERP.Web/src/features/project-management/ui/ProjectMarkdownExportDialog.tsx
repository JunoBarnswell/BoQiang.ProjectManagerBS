import { Box, Stack, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';

import { exportProjectManagementProjectMarkdown, getProjectManagementTasks } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { useMessage } from '../../../shared/feedback/useMessage';
import { ResponsiveModal } from '../../../shared/responsive/ResponsiveModal';
import { DataTable } from '../../../shared/table/DataTable';
import type { DataTableQueryState } from '../../../shared/table/tableTypes';
import { projectManagementEnumLabel, useProjectManagementI18n } from '../projectManagementI18n';
import { PROJECT_MANAGEMENT_TASK_STATUSES } from '../state/projectManagementStatusTransitions';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

const exportPageSize = 20;
const exportAllIdsPageSize = 200;

const defaultTableQuery: DataTableQueryState = { matchMode: 'and', conditions: [] };

function readStatusFilter(tableQuery: DataTableQueryState): string | undefined {
  const condition = tableQuery.conditions.find(
    (item) => item.field === 'status' && item.operator === 'equals' && item.value !== undefined && item.value !== '',
  );
  return condition ? String(condition.value) : undefined;
}

async function fetchAllProjectTaskIds(projectId: string, status: string | undefined, signal?: AbortSignal) {
  const ids: string[] = [];
  let pageIndex = 1;
  let total = 0;

  do {
    const response = await getProjectManagementTasks({
      projectId,
      pageIndex,
      pageSize: exportAllIdsPageSize,
      viewKey: 'list',
      includeCompleted: true,
      status,
    }, signal);
    total = response.data.total;
    ids.push(...response.data.items.map((task) => task.id));
    pageIndex += 1;
  } while (ids.length < total);

  return { ids, total };
}

export function ProjectMarkdownExportDialog({
  onClose,
  open,
  projectId,
}: {
  onClose: () => void;
  open: boolean;
  projectId: string;
}) {
  const { format, t } = useProjectManagementI18n();
  const message = useMessage();
  const scope = useProjectManagementWorkspaceScope();
  const [pageIndex, setPageIndex] = useState(1);
  const [tableQuery, setTableQuery] = useState<DataTableQueryState>(defaultTableQuery);
  const [selectedTaskIds, setSelectedTaskIds] = useState<string[]>([]);
  const [includeProjectInfo, setIncludeProjectInfo] = useState(true);
  const [exporting, setExporting] = useState(false);

  const statusFilter = readStatusFilter(tableQuery);

  const listQuery = {
    projectId,
    pageIndex,
    pageSize: exportPageSize,
    viewKey: 'list' as const,
    includeCompleted: true,
    status: statusFilter,
  };

  const tasksQuery = useQuery({
    enabled: open && scope.isAvailable && Boolean(projectId),
    queryKey: projectManagementQueryKeys.tasks(scope, listQuery),
    queryFn: ({ signal }) => getProjectManagementTasks(listQuery, signal),
  });

  const allTaskIdsQuery = useQuery({
    enabled: open && scope.isAvailable && Boolean(projectId),
    queryKey: [...projectManagementQueryKeys.tasksProject(scope, projectId), 'markdown-export-all-ids', statusFilter ?? ''] as const,
    queryFn: ({ signal }) => fetchAllProjectTaskIds(projectId, statusFilter, signal),
  });

  const rows = tasksQuery.data?.data.items ?? [];
  const total = tasksQuery.data?.data.total ?? allTaskIdsQuery.data?.total ?? 0;
  const allTaskIds = allTaskIdsQuery.data?.ids ?? [];
  const allSelected = total > 0 && selectedTaskIds.length === total && allTaskIds.every((id) => selectedTaskIds.includes(id));
  const partiallySelected = selectedTaskIds.length > 0 && !allSelected;

  const statusFilterOptions = useMemo(
    () => PROJECT_MANAGEMENT_TASK_STATUSES.map((status) => ({
      label: projectManagementEnumLabel(t, 'status', status),
      value: status,
    })),
    [t],
  );

  const columns = useMemo(() => [
    {
      key: 'code',
      title: t('projectManagement.workItems.column.code'),
      width: '120px',
      render: (row: ProjectManagementTaskListItem) => row.taskCode,
    },
    {
      key: 'title',
      title: t('projectManagement.workItems.column.title'),
      render: (row: ProjectManagementTaskListItem) => row.title,
    },
    {
      key: 'status',
      title: t('projectManagement.workItems.column.status'),
      width: '130px',
      binding: 'status',
      filterField: 'status',
      filterable: true,
      filterType: 'select' as const,
      filterOptions: statusFilterOptions,
      render: (row: ProjectManagementTaskListItem) => projectManagementEnumLabel(t, 'status', row.status),
    },
  ], [statusFilterOptions, t]);

  useEffect(() => {
    if (!open) {
      setPageIndex(1);
      setTableQuery(defaultTableQuery);
      setSelectedTaskIds([]);
      setIncludeProjectInfo(true);
      setExporting(false);
    }
  }, [open]);

  useEffect(() => {
    if (!open || !allTaskIdsQuery.isSuccess) return;
    setSelectedTaskIds(allTaskIdsQuery.data.ids);
  }, [allTaskIdsQuery.data?.ids, allTaskIdsQuery.isSuccess, open, statusFilter]);

  const handleQueryChange = (next: DataTableQueryState) => {
    setTableQuery(next);
    setPageIndex(1);
  };

  const toggleAll = (checked: boolean) => {
    setSelectedTaskIds(checked ? allTaskIds : []);
  };

  const exportMarkdown = async () => {
    if (exporting || selectedTaskIds.length === 0) return;
    setExporting(true);
    try {
      const { blob, fileName } = await exportProjectManagementProjectMarkdown(projectId, {
        includeProjectInfo,
        taskIds: selectedTaskIds,
      });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = fileName;
      anchor.click();
      URL.revokeObjectURL(url);
      message.success(t('projectManagement.workbench.overview.exportSuccess'));
      onClose();
    } catch (error) {
      message.error(error instanceof Error ? error.message : t('projectManagement.workbench.overview.exportFailed'));
    } finally {
      setExporting(false);
    }
  };

  const footer = (
    <Stack alignItems="center" className="pm-markdown-export-dialog__footer" direction="row" justifyContent="flex-end" spacing={1} width="100%">
      <button className="pm-workbench-command" disabled={exporting} onClick={onClose} type="button">{t('projectManagement.editor.cancel')}</button>
      <button className="pm-primary-button" disabled={exporting || selectedTaskIds.length === 0 || tasksQuery.isLoading || allTaskIdsQuery.isLoading} onClick={() => void exportMarkdown()} type="button">
        {t('projectManagement.workbench.overview.exportMarkdown')}
      </button>
    </Stack>
  );

  return (
    <ResponsiveModal
      bodyClassName="pm-markdown-export-dialog-body"
      className="pm-markdown-export-dialog"
      footer={footer}
      maxWidth={880}
      mode="modal"
      onClose={onClose}
      open={open}
      title={t('projectManagement.workbench.overview.exportDialogTitle')}
    >
      <Stack className="pm-markdown-export-dialog" spacing={1.25}>
        <label className="pm-markdown-export-dialog__option">
          <input checked={includeProjectInfo} onChange={(event) => setIncludeProjectInfo(event.target.checked)} type="checkbox" />
          <span>{t('projectManagement.workbench.overview.exportIncludeProjectInfo')}</span>
        </label>

        <Box className="pm-markdown-export-dialog__toolbar">
          <label className="pm-markdown-export-dialog__select-all">
            <input
              checked={allSelected}
              disabled={allTaskIdsQuery.isLoading || total === 0}
              onChange={(event) => toggleAll(event.target.checked)}
              ref={(element) => {
                if (element) element.indeterminate = partiallySelected;
              }}
              type="checkbox"
            />
            <span>{t('projectManagement.workbench.overview.exportSelectAll')}</span>
          </label>
          <Typography color="text.secondary" variant="caption">
            {format('projectManagement.workbench.overview.exportSelectedCount', { selected: selectedTaskIds.length, total })}
          </Typography>
        </Box>

        <Box className="pm-markdown-export-dialog__table">
          <DataTable<ProjectManagementTaskListItem>
            className="pm-markdown-export-table"
            columns={columns}
            emptyText={t('projectManagement.workbench.overview.exportTasksEmpty')}
            loading={tasksQuery.isLoading}
            onPageChange={setPageIndex}
            onQueryChange={handleQueryChange}
            pagination={{ current: pageIndex, pageSize: exportPageSize, total }}
            rowKey={(row) => row.id}
            rows={rows}
            selection={{ selectedRowKeys: selectedTaskIds, onChange: setSelectedTaskIds }}
            tableQuery={tableQuery}
          />
        </Box>
      </Stack>
    </ResponsiveModal>
  );
}
