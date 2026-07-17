import { Plus, Search } from 'lucide-react';
import { useState } from 'react';


import {
  deleteApplicationDataSourceTableRow,
  getApplicationDataSourceWorkbenchTable,
  insertApplicationDataSourceTableRow,
  queryApplicationDataSourceTableRows,
  updateApplicationDataSourceTableRow
} from '../../api/application-data-center/applicationDataCenter.api';
import type {
  ApplicationDataSourceTableRowDeleteRequest,
  ApplicationDataSourceTableRowUpsertRequest,
  ApplicationDataSourceTableRowsResponse
} from '../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../core/i18n/I18nProvider';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useApiQuery } from '../../core/query/useApiQuery';
import { useWorkspaceStore } from '../../core/state';
import { PermissionButton } from '../auth/PermissionButton';
import { useConfirm } from '../feedback/useConfirm';
import { useMessage } from '../feedback/useMessage';
import { ResponsiveModal } from '../responsive/ResponsiveModal';
import { PageError } from '../status/PageError';
import { PageLoading } from '../status/PageLoading';
import { getErrorMessage } from '../utils/errorMessage';

import {
  buildTableRowDeleteRequest,
  buildTableRowInsertRequest,
  buildTableRowUpdateRequest
} from './tableRowMutation';
import { WorkbenchDataGrid } from './WorkbenchDataGrid';
import { WorkbenchRowDataForm } from './WorkbenchRowDataForm';

interface DataSourceTableEditorProps {
  dataSourceId?: string | null;
  editable?: boolean | null;
  pageSize?: number | null;
  tableName?: string | null;
  title?: string | null;
}

export function DataSourceTableEditor({
  dataSourceId,
  editable = true,
  pageSize = 20,
  tableName,
  title
}: DataSourceTableEditorProps) {
  const message = useMessage();
  const confirm = useConfirm();
  const currentWorkspace = useWorkspaceStore((state) => state.currentWorkspace);
  const [keyword, setKeyword] = useState('');
  const [pageIndex, setPageIndex] = useState(1);
  const [rowForm, setRowForm] = useState<{ originalRow?: Record<string, unknown>; values: Record<string, unknown> } | null>(null);
  const [savingCell, setSavingCell] = useState<{ fieldCode: string; rowIndex: number } | null>(null);
  const normalizedDataSourceId = String(dataSourceId ?? '').trim();
  const normalizedTableName = String(tableName ?? '').trim();
  const resolvedPageSize = Math.max(1, Number(pageSize ?? 20));

  const detailQuery = useApiQuery({
    enabled: Boolean(normalizedDataSourceId && normalizedTableName),
    queryFn: ({ signal }) => getApplicationDataSourceWorkbenchTable(normalizedDataSourceId, normalizedTableName, signal),
    queryKey: ['data-source-table-editor', currentWorkspace?.tenantId ?? '', currentWorkspace?.appCode ?? '', normalizedDataSourceId, normalizedTableName, 'detail']
  });
  const rowsQuery = useApiQuery({
    enabled: Boolean(normalizedDataSourceId && normalizedTableName),
    queryFn: ({ signal }) => queryApplicationDataSourceTableRows(normalizedDataSourceId, normalizedTableName, { keyword, pageIndex, pageSize: resolvedPageSize }, signal),
    queryKey: ['data-source-table-editor', currentWorkspace?.tenantId ?? '', currentWorkspace?.appCode ?? '', normalizedDataSourceId, normalizedTableName, keyword, pageIndex, resolvedPageSize]
  });

  const insertRowMutation = useApiMutation({
    mutationFn: (request: ApplicationDataSourceTableRowUpsertRequest) => insertApplicationDataSourceTableRow(normalizedDataSourceId, normalizedTableName, request),
    onError: (error) => message.error(getErrorMessage(error, '新增行失败')),
    onSuccess: async () => {
      message.success('行数据已新增');
      setRowForm(null);
      await rowsQuery.refetch();
    }
  });
  const updateRowMutation = useApiMutation({
    mutationFn: (request: ApplicationDataSourceTableRowUpsertRequest) => updateApplicationDataSourceTableRow(normalizedDataSourceId, normalizedTableName, request),
    onError: (error) => message.error(getErrorMessage(error, '编辑行失败')),
    onSuccess: async () => {
      message.success('行数据已保存');
      setRowForm(null);
      await rowsQuery.refetch();
    }
  });

  if (!normalizedDataSourceId || !normalizedTableName) {
    return <PageError description="数据源表编辑器缺少 dataSourceId 或 tableName。" />;
  }

  if (detailQuery.isLoading && !detailQuery.data) {
    return <PageLoading />;
  }

  if (detailQuery.isError) {
    return <PageError description={getErrorMessage(detailQuery.error, '数据表结构加载失败')} />;
  }

  if (rowsQuery.isError) {
    return <PageError description={getErrorMessage(rowsQuery.error, '行数据加载失败')} />;
  }

  const rows = rowsQuery.data?.data as ApplicationDataSourceTableRowsResponse | undefined;
  const canEdit = Boolean(editable && rows?.editable);

  return (
    <div className="grid gap-3">
      <div className="flex flex-col gap-2 rounded-md border border-slate-200 bg-slate-50/70 p-2 lg:flex-row lg:items-center lg:justify-between">
        <div className="min-w-0">
          <div className="text-sm font-semibold text-slate-950">{title || normalizedTableName}</div>
          <div className="text-xs text-slate-500">
            主键 {rows?.primaryKeys.length ? rows.primaryKeys.join(', ') : '无'} · 共 {rows?.total ?? 0} 行
          </div>
        </div>
        <label className="relative min-w-0 flex-1 lg:max-w-md">
          <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-400" />
          <input
            className="form-input h-8 pl-8 text-xs"
            placeholder={translateCurrentLiteral("按关键字查询行数据")}
            value={keyword}
            onChange={(event) => {
              setPageIndex(1);
              setKeyword(event.target.value);
            }}
          />
        </label>
        <div className="flex flex-wrap items-center gap-2">
          <PermissionButton className="secondary-button h-8 text-xs" code="app:data-center:data-source:data-query" type="button" onClick={() => rowsQuery.refetch()}>
            <Search className="h-3.5 w-3.5" />{translateCurrentLiteral("查询数据")}</PermissionButton>
          <PermissionButton className="primary-button h-8 text-xs" code="app:data-center:data-source:data-edit" disabled={!canEdit || !rows?.canInsert} type="button" onClick={openInsertRow}>
            <Plus className="h-3.5 w-3.5" />{translateCurrentLiteral("新增行")}</PermissionButton>
        </div>
      </div>

      <WorkbenchDataGrid
        editable={canEdit}
        editDisabledReason={rows?.editDisabledReason}
        fields={rows?.fields ?? []}
        primaryKeys={rows?.primaryKeys ?? []}
        rows={rows?.rows ?? []}
        savingCell={savingCell}
        onCellCommit={commitCell}
        onDelete={deleteRow}
        onEdit={openEditRow}
      />
      <div className="flex justify-end gap-2">
        <button className="secondary-button h-7 text-xs" disabled={pageIndex <= 1} type="button" onClick={() => setPageIndex((value) => Math.max(1, value - 1))}>{translateCurrentLiteral("上一页")}</button>
        <span className="flex h-7 items-center text-xs text-slate-500">第 {pageIndex} 页 / 共 {rows?.total ?? 0} 行</span>
        <button className="secondary-button h-7 text-xs" disabled={!rows || pageIndex * rows.pageSize >= rows.total} type="button" onClick={() => setPageIndex((value) => value + 1)}>{translateCurrentLiteral("下一页")}</button>
      </div>

      <ResponsiveModal
        footer={<><button className="secondary-button" type="button" onClick={() => setRowForm(null)}>{translateCurrentLiteral("取消")}</button><button className="primary-button" disabled={!rowForm || insertRowMutation.isPending || updateRowMutation.isPending} type="button" onClick={saveRow}>{rowForm?.originalRow ? '保存修改' : '保存'}</button></>}
        mode="drawer"
        open={Boolean(rowForm)}
        title={rowForm?.originalRow ? '编辑行数据' : '新增行数据'}
        onClose={() => setRowForm(null)}
      >
        {rowForm ? (
          <WorkbenchRowDataForm
            columns={detailQuery.data?.data.columns ?? []}
            mode={rowForm.originalRow ? 'edit' : 'create'}
            primaryKeys={rows?.primaryKeys ?? []}
            values={rowForm.values}
            onChange={(values) => setRowForm({ ...rowForm, values })}
          />
        ) : null}
      </ResponsiveModal>
    </div>
  );

  function openInsertRow() {
    const values = Object.fromEntries((detailQuery.data?.data.columns ?? []).map((column) => [column.columnName, '']));
    setRowForm({ values });
  }

  function openEditRow(row: Record<string, unknown>) {
    setRowForm({ originalRow: { ...row }, values: { ...row } });
  }

  function commitCell(row: Record<string, unknown>, fieldCode: string, value: unknown) {
    if (!rows?.primaryKeys.length) {
      message.error('当前表没有主键，无法定位行数据');
      return;
    }

    const rowIndex = rows.rows.indexOf(row);
    const values = { ...row, [fieldCode]: value };
    setSavingCell({ fieldCode, rowIndex });
    updateRowMutation.mutate(
      buildTableRowUpdateRequest(row, values, {
        concurrencyColumn: rows.concurrencyColumn,
        primaryKeys: rows.primaryKeys
      }),
      {
        onSettled: () => setSavingCell(null)
      }
    );
  }

  function saveRow() {
    if (!rowForm) {
      return;
    }

    if (rowForm.originalRow) {
      updateRowMutation.mutate(buildTableRowUpdateRequest(rowForm.originalRow, rowForm.values, {
        concurrencyColumn: rows?.concurrencyColumn,
        primaryKeys: rows?.primaryKeys ?? []
      }));
      return;
    }

    insertRowMutation.mutate(buildTableRowInsertRequest(rowForm.values));
  }

  function deleteRow(row: Record<string, unknown>) {
    confirm({
      content: '确认删除当前行数据？',
      confirmText: '删除',
      title: '删除行数据',
      onConfirm: async () => {
        const request: ApplicationDataSourceTableRowDeleteRequest = buildTableRowDeleteRequest(row, {
          concurrencyColumn: rows?.concurrencyColumn,
          primaryKeys: rows?.primaryKeys ?? []
        });
        await deleteApplicationDataSourceTableRow(normalizedDataSourceId, normalizedTableName, request);
        message.success('行数据已删除');
        await rowsQuery.refetch();
      }
    });
  }
}
