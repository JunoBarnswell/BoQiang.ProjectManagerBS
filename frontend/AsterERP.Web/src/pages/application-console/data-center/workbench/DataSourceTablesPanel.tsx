import { Braces, ChevronDown, ChevronRight, Database, DatabaseZap, Folder, Plus, RefreshCw, Search, Star, Table2 } from 'lucide-react';
import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useParams } from 'react-router-dom';


import {
  createApplicationDataSourceAlterTablePlan,
  createApplicationDataSourceWorkbenchTable,
  deployApplicationDataSourceAlterTablePlan,
  deployApplicationDataSourceSchemaChangePlan,
  deleteApplicationDataSourceTableRow,
  streamApplicationDataSourceTableRowsExport,
  getApplicationDataSourceWorkbenchTable,
  insertApplicationDataSourceTableRow,
  listApplicationDataSourceWorkbenchTables,
  refreshApplicationDataSourceCatalogNode,
  queryApplicationDataSourceTableRows,
  updateApplicationDataSourceTableRow
} from '../../../../api/application-data-center/applicationDataCenter.api';
import type {
  ApplicationDataSourceCreateTableColumnRequest,
  ApplicationDataSourceCreateTableRequest,
  ApplicationDataSourceAlterTablePlanRequest,
  ApplicationDataSourceAlterTableRequest,
  ApplicationDataSourceTableRowDeleteRequest,
  ApplicationDataSourceTableRowsExportRequest,
  ApplicationDataSourceTableRowFilterRequest,
  ApplicationDataSourceTableRowSortRequest,
  ApplicationDataSourceSchemaChangePlanResponse,
  ApplicationDataSourceTable,
  ApplicationDataSourceTableRowMutationResponse,
  ApplicationDataSourceTableRowsResponse,
  ApplicationDataSourceTableRowUpsertRequest
} from '../../../../api/application-data-center/applicationDataCenter.types';
import { isHttpError } from '../../../../core/http/httpError';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../../core/query/useApiMutation';
import { useApiQuery } from '../../../../core/query/useApiQuery';
import { PermissionButton } from '../../../../shared/auth/PermissionButton';
import { WorkbenchDataGrid, WorkbenchRowDataForm } from '../../../../shared/data-source-table-editor';
import { readRowConflictPayload } from '../../../../shared/data-source-table-editor/rowConflict';
import {
  buildTableRowDeleteRequest,
  buildTableRowInsertRequest,
  buildTableRowUpdateRequest
} from '../../../../shared/data-source-table-editor/tableRowMutation';
import { useConfirm } from '../../../../shared/feedback/useConfirm';
import { useMessage } from '../../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../../shared/utils/errorMessage';

import { WorkbenchDrawerForm } from './components/WorkbenchDrawerForm';
import { WorkbenchFieldGrid } from './components/WorkbenchFieldGrid';
import { qualifiedTableName } from './workbenchTypes';

interface DataSourceTablesPanelProps {
  dataSourceId: string;
  selectedTable: string;
  onSelectedTableChange: (tableName: string) => void;
  onRefresh: () => void;
}

const defaultColumn = (): ApplicationDataSourceCreateTableColumnRequest => ({
  columnName: '',
  dataType: 'TEXT',
  defaultValue: '',
  nullable: true,
  primaryKey: false,
  remark: ''
});

export function DataSourceTablesPanel({ dataSourceId, selectedTable, onSelectedTableChange, onRefresh }: DataSourceTablesPanelProps) {
  const { appCode, tenantId } = useParams();
  const message = useMessage();
  const confirm = useConfirm();
  const [keyword, setKeyword] = useState('');
  const [typeFilter, setTypeFilter] = useState<'all' | 'table' | 'view'>('all');
  const [scopeFilter, setScopeFilter] = useState<'all' | 'favorites' | 'recent'>('all');
  const [favoriteNames, setFavoriteNames] = useState<Set<string>>(() => readObjectNames(dataSourceId, 'favorites'));
  const [recentNames, setRecentNames] = useState<string[]>(() => readRecentNames(dataSourceId));
  const [openObjects, setOpenObjects] = useState<string[]>(() => selectedTable ? [selectedTable] : []);
  const [rowKeyword, setRowKeyword] = useState('');
  const [pageIndex, setPageIndex] = useState(1);
  const [rowSort, setRowSort] = useState<ApplicationDataSourceTableRowSortRequest | null>(null);
  const [rowFilters, setRowFilters] = useState<ApplicationDataSourceTableRowFilterRequest[]>([]);
  const [filterField, setFilterField] = useState('');
  const [filterOperator, setFilterOperator] = useState<ApplicationDataSourceTableRowFilterRequest['operator']>('contains');
  const [filterValue, setFilterValue] = useState('');
  const [collapsedSchemas, setCollapsedSchemas] = useState<Set<string>>(() => new Set());
  const [collapsedGroups, setCollapsedGroups] = useState<Set<string>>(() => new Set());
  const [tableForm, setTableForm] = useState<ApplicationDataSourceCreateTableRequest | null>(null);
  const [tablePlan, setTablePlan] = useState<ApplicationDataSourceSchemaChangePlanResponse | null>(null);
  const [alterTableForm, setAlterTableForm] = useState<ApplicationDataSourceAlterTableRequest | null>(null);
  const [alterTablePlan, setAlterTablePlan] = useState<ApplicationDataSourceSchemaChangePlanResponse | null>(null);
  const [rowForm, setRowForm] = useState<{ originalRow?: Record<string, unknown>; values: Record<string, unknown> } | null>(null);
  const [rowConflict, setRowConflict] = useState<RowConflict | null>(null);
  const [savingCell, setSavingCell] = useState<{ fieldCode: string; rowIndex: number } | null>(null);

  const refreshNodeMutation = useApiMutation({
    mutationFn: (table: ApplicationDataSourceTable) => refreshApplicationDataSourceCatalogNode(dataSourceId, { schemaName: table.schemaName, tableName: table.tableName }),
    onSuccess: async () => {
      await tablesQuery.refetch();
      if (selectedTable) await detailQuery.refetch();
      onRefresh();
      message.success('Object refreshed');
    },
    onError: (error) => message.error(getErrorMessage(error, 'Object refresh failed'))
  });

  useEffect(() => {
    setFavoriteNames(readObjectNames(dataSourceId, 'favorites'));
    setRecentNames(readRecentNames(dataSourceId));
    setOpenObjects([]);
  }, [dataSourceId]);

  useEffect(() => {
    if (selectedTable) {
      setOpenObjects((current) => current.includes(selectedTable) ? current : [...current, selectedTable]);
    }
  }, [selectedTable]);

  const tablesQuery = useApiQuery({
    enabled: Boolean(dataSourceId),
    queryFn: ({ signal }) => listApplicationDataSourceWorkbenchTables(dataSourceId, signal),
    queryKey: ['application-data-center', tenantId ?? '', appCode ?? '', 'workbench', dataSourceId, 'tables']
  });
  const detailQuery = useApiQuery({
    enabled: Boolean(dataSourceId && selectedTable),
    queryFn: ({ signal }) => getApplicationDataSourceWorkbenchTable(dataSourceId, selectedTable, signal),
    queryKey: ['application-data-center', tenantId ?? '', appCode ?? '', 'workbench', dataSourceId, 'table', selectedTable]
  });
  const rowsQuery = useApiQuery({
    enabled: Boolean(dataSourceId && selectedTable),
    queryFn: ({ signal }) => queryApplicationDataSourceTableRows(dataSourceId, selectedTable, { filters: rowFilters, keyword: rowKeyword, pageIndex, pageSize: 20, sorts: rowSort ? [rowSort] : [] }, signal),
    queryKey: ['application-data-center', tenantId ?? '', appCode ?? '', 'workbench', dataSourceId, 'table-rows', selectedTable, rowKeyword, pageIndex, rowFilters, rowSort]
  });

  useEffect(() => {
    if (!selectedTable && tablesQuery.data?.data?.length) {
      onSelectedTableChange(qualifiedTableName(tablesQuery.data.data[0]));
    }
  }, [onSelectedTableChange, selectedTable, tablesQuery.data?.data]);

  const filteredTables = useMemo(() => {
    const value = keyword.trim().toLowerCase();
    return (tablesQuery.data?.data ?? []).filter((table) => {
      const name = qualifiedTableName(table);
      const isFavorite = favoriteNames.has(name);
      const isRecent = recentNames.includes(name);
      const matchesScope = scopeFilter === 'all' || (scopeFilter === 'favorites' ? isFavorite : isRecent);
      const matchesType = typeFilter === 'all' || (typeFilter === 'view' ? isView(table) : !isView(table));
      return matchesScope && matchesType && (!value || name.toLowerCase().includes(value) || table.tableType.toLowerCase().includes(value));
    });
  }, [favoriteNames, keyword, recentNames, scopeFilter, tablesQuery.data?.data, typeFilter]);
  const tree = useMemo(() => buildTableTree(filteredTables), [filteredTables]);

  const createTablePlanMutation = useApiMutation({
    mutationFn: (request: ApplicationDataSourceCreateTableRequest) => createApplicationDataSourceWorkbenchTable(dataSourceId, request),
    onError: (error) => message.error(getErrorMessage(error, translateCurrentLiteral('创建表失败'))),
    onSuccess: (response) => {
      setTablePlan(response.data);
      message.success(translateCurrentLiteral('SchemaChangePlan 已创建，请先审核风险和 SQL 预览。'));
    }
  });
  const deployTablePlanMutation = useApiMutation({
    mutationFn: (plan: ApplicationDataSourceSchemaChangePlanResponse) => deployApplicationDataSourceSchemaChangePlan(dataSourceId, {
      confirmed: true,
      planHash: plan.planHash,
      table: tableForm as ApplicationDataSourceCreateTableRequest
    }),
    onError: (error) => message.error(getErrorMessage(error, translateCurrentLiteral('DDL 部署失败'))),
    onSuccess: (response) => {
      message.success(translateCurrentLiteral('数据表已按已确认计划创建'));
      onSelectedTableChange(qualifiedTableName(response.data.table));
      setTablePlan(null);
      setTableForm(null);
      onRefresh();
    }
  });
  const createAlterTablePlanMutation = useApiMutation({
    mutationFn: (request: ApplicationDataSourceAlterTableRequest) => createApplicationDataSourceAlterTablePlan(dataSourceId, request),
    onError: (error) => message.error(getErrorMessage(error, translateCurrentLiteral('Failed to create schema change plan'))),
    onSuccess: (response) => {
      setAlterTablePlan(response.data);
      message.success(translateCurrentLiteral('SchemaChangePlan created; review risk and SQL before deployment.'));
    }
  });
  const deployAlterTablePlanMutation = useApiMutation({
    mutationFn: (plan: ApplicationDataSourceSchemaChangePlanResponse) => deployApplicationDataSourceAlterTablePlan(dataSourceId, {
      confirmed: true,
      planHash: plan.planHash,
      table: alterTableForm as ApplicationDataSourceAlterTableRequest
    } satisfies ApplicationDataSourceAlterTablePlanRequest),
    onError: (error) => message.error(getErrorMessage(error, translateCurrentLiteral('Failed to deploy schema change'))),
    onSuccess: async () => {
      message.success(translateCurrentLiteral('Table schema updated.'));
      setAlterTableForm(null);
      setAlterTablePlan(null);
      await detailQuery.refetch();
      await tablesQuery.refetch();
      onRefresh();
    }
  });
  const insertRowMutation = useApiMutation({
    mutationFn: (request: { confirmed: boolean; values: Record<string, unknown> }) => insertApplicationDataSourceTableRow(dataSourceId, selectedTable, request),
    onError: (error) => message.error(getErrorMessage(error, translateCurrentLiteral('新增行失败'))),
    onSuccess: () => {
      message.success(translateCurrentLiteral('行数据已新增'));
      setRowForm(null);
      rowsQuery.refetch();
    }
  });
  const updateRowMutation = useApiMutation({
    mutationFn: (request: ReturnType<typeof buildTableRowUpdateRequest>) => updateApplicationDataSourceTableRow(dataSourceId, selectedTable, request),
    onError: (error) => message.error(getErrorMessage(error, translateCurrentLiteral('编辑行失败'))),
    onSuccess: () => {
      message.success(translateCurrentLiteral('行数据已保存'));
      setRowForm(null);
      rowsQuery.refetch();
    }
  });
  const rows = rowsQuery.data?.data as ApplicationDataSourceTableRowsResponse | undefined;
  return (
    <section className="grid gap-3 xl:grid-cols-[320px_1fr]">
      <aside className="min-h-[460px] overflow-hidden rounded-md border border-slate-200 bg-white">
        <div className="border-b border-slate-100 p-3">
          <div className="mb-2 flex gap-2">
            <label className="relative min-w-0 flex-1">
              <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-400" />
              <input className="form-input h-8 pl-8 text-xs" placeholder={translateCurrentLiteral("搜索表/视图")} value={keyword} onChange={(event) => setKeyword(event.target.value)} />
            </label>
          </div>
          <div className="mb-2 grid grid-cols-2 gap-2">
            <select className="form-input h-8 text-xs" value={typeFilter} onChange={(event) => setTypeFilter(event.target.value as typeof typeFilter)}>
              <option value="all">{translateCurrentLiteral('全部类型')}</option>
              <option value="table">Tables</option>
              <option value="view">Views</option>
            </select>
            <select className="form-input h-8 text-xs" value={scopeFilter} onChange={(event) => setScopeFilter(event.target.value as typeof scopeFilter)}>
              <option value="all">{translateCurrentLiteral('全部对象')}</option>
              <option value="favorites">{translateCurrentLiteral('收藏')}</option>
              <option value="recent">{translateCurrentLiteral('最近使用')}</option>
            </select>
          </div>
          <PermissionButton className="primary-button h-8 w-full justify-center text-xs" code="app:data-center:data-source:edit" type="button" onClick={openCreateTable}>
            <DatabaseZap className="h-3.5 w-3.5" />{translateCurrentLiteral("新建表")}</PermissionButton>
        </div>
        <div className="data-source-tables-panel__tree overflow-y-auto px-2 py-2">
          {tree.map((schema) => (
            <div className="mb-2" key={schema.name}>
              <button className="flex h-7 w-full items-center gap-1.5 rounded px-2 text-xs font-semibold text-slate-800 transition hover:bg-slate-50" type="button" onClick={() => toggleSchema(schema.name)}>
                {collapsedSchemas.has(schema.name) ? <ChevronRight className="h-3.5 w-3.5 text-slate-400" /> : <ChevronDown className="h-3.5 w-3.5 text-slate-400" />}
                <Database className="h-3.5 w-3.5 text-primary-500" />
                <span className="truncate">{schema.name}</span>
              </button>
              {collapsedSchemas.has(schema.name) ? null : (
                <>
                  <TreeGroup
                    collapsed={collapsedGroups.has(`${schema.name}:tables`)}
                    count={schema.tables.length}
                    icon={<Folder className="h-4 w-4 text-amber-500" />}
                    title="Tables"
                    onToggle={() => toggleGroup(`${schema.name}:tables`)}
                  >
                    {schema.tables.map((table) => renderTreeItem(table, <Table2 className="h-4 w-4 text-primary-500" />))}
                  </TreeGroup>
                  <TreeGroup
                    collapsed={collapsedGroups.has(`${schema.name}:views`)}
                    count={schema.views.length}
                    icon={<Folder className="h-4 w-4 text-amber-500" />}
                    title="Views"
                    onToggle={() => toggleGroup(`${schema.name}:views`)}
                  >
                    {schema.views.map((table) => renderTreeItem(table, <Braces className="h-4 w-4 text-violet-500" />))}
                  </TreeGroup>
                </>
              )}
            </div>
          ))}
          {tree.length === 0 ? <EmptyState text={translateCurrentLiteral('暂无表或视图')} /> : null}
        </div>
      </aside>
      <div className="min-w-0 space-y-3 rounded-md border border-slate-200 bg-white p-3">
        {rowConflict ? (
          <RowConflictPanel
            conflict={rowConflict}
            onDismiss={() => setRowConflict(null)}
            onRetry={() => retryRowConflict(rowConflict)}
            onOverwrite={() => overwriteRowConflict(rowConflict)}
          />
        ) : null}
        {openObjects.length > 0 ? (
          <div className="flex min-w-0 items-center gap-1 overflow-x-auto border-b border-slate-100 pb-2" role="tablist" aria-label={translateCurrentLiteral('打开的数据对象')}>
            {openObjects.map((name) => (
              <div className={`flex shrink-0 items-center rounded border text-xs ${selectedTable === name ? 'border-primary-200 bg-primary-50 text-primary-700' : 'border-slate-200 text-slate-600'}`} key={name}>
                <button className="max-w-48 truncate px-2 py-1.5" role="tab" type="button" aria-selected={selectedTable === name} onClick={() => onSelectedTableChange(name)}>{name}</button>
                <button className="px-1.5 py-1.5 text-slate-400 hover:text-red-600" type="button" aria-label={`${translateCurrentLiteral('关闭')} ${name}`} onClick={() => closeObject(name)}>×</button>
              </div>
            ))}
          </div>
        ) : null}
        {detailQuery.data?.data ? (
          <>
            <WorkbenchFieldGrid
              columns={3}
              fields={[
                { label: translateCurrentLiteral("表名"), value: qualifiedTableName(detailQuery.data.data.table) },
                { label: translateCurrentLiteral("类型"), value: detailQuery.data.data.table.tableType },
                { label: translateCurrentLiteral("字段数"), value: detailQuery.data.data.columns.length },
                { label: translateCurrentLiteral("主键"), value: detailQuery.data.data.columns.filter((column) => column.primaryKey).map((column) => column.columnName).join(', ') || translateCurrentLiteral('无') }
              ]}
            />
            <div className="flex justify-end">
              <PermissionButton className="secondary-button h-8 text-xs" code="app:data-center:data-source:edit" type="button" onClick={openAlterTable}>
                <DatabaseZap className="h-3.5 w-3.5" />{translateCurrentLiteral('Edit table schema')}
              </PermissionButton>
            </div>
            <div className="flex flex-col gap-2 rounded-md border border-slate-200 bg-slate-50/70 p-2 lg:flex-row lg:items-center lg:justify-between">
              <label className="relative min-w-0 flex-1 lg:max-w-md">
                <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-400" />
                <input className="form-input h-8 pl-8 text-xs" placeholder={translateCurrentLiteral("按关键字查询行数据")} value={rowKeyword} onChange={(event) => { setPageIndex(1); setRowKeyword(event.target.value); }} />
              </label>
              <div className="flex flex-wrap items-center gap-2">
                <PermissionButton className="secondary-button h-8 text-xs" code="app:data-center:data-source:data-query" type="button" onClick={() => rowsQuery.refetch()}>
                  <Search className="h-3.5 w-3.5" />{translateCurrentLiteral("查询数据")}</PermissionButton>
                <PermissionButton className="primary-button h-8 text-xs" code="app:data-center:data-source:data-edit" disabled={!rows?.canInsert} type="button" onClick={openInsertRow}>
                  <Plus className="h-3.5 w-3.5" />{translateCurrentLiteral("新增行")}</PermissionButton>
              </div>
            </div>
            <div className="grid gap-2 rounded-md border border-slate-200 bg-white p-2 md:grid-cols-[1.2fr_1fr_1fr_auto]">
              <select className="form-input h-8 text-xs" value={filterField} onChange={(event) => setFilterField(event.target.value)} aria-label={translateCurrentLiteral('筛选字段')}>
                <option value="">{translateCurrentLiteral('选择筛选字段')}</option>
                {rows?.fields.map((field) => <option key={field.fieldCode} value={field.fieldCode}>{field.fieldName || field.fieldCode}</option>)}
              </select>
              <select className="form-input h-8 text-xs" value={filterOperator} onChange={(event) => setFilterOperator(event.target.value as typeof filterOperator)} aria-label={translateCurrentLiteral('筛选条件')}>
                <option value="contains">{translateCurrentLiteral('包含')}</option>
                <option value="equals">{translateCurrentLiteral('等于')}</option>
                <option value="notEquals">{translateCurrentLiteral('不等于')}</option>
                <option value="gt">&gt;</option>
                <option value="gte">&gt;=</option>
                <option value="lt">&lt;</option>
                <option value="lte">&lt;=</option>
              </select>
              <input className="form-input h-8 text-xs" value={filterValue} onChange={(event) => setFilterValue(event.target.value)} placeholder={translateCurrentLiteral('筛选值')} aria-label={translateCurrentLiteral('筛选值')} />
              <button className="secondary-button h-8 text-xs" type="button" disabled={!filterField || !filterValue.trim()} onClick={addRowFilter}>{translateCurrentLiteral('添加条件')}</button>
            </div>
            {rowFilters.length > 0 ? <div className="flex flex-wrap gap-1.5 text-xs">{rowFilters.map((filter, index) => <span className="inline-flex items-center gap-1 rounded bg-slate-100 px-2 py-1 text-slate-600" key={`${filter.fieldCode}-${index}`}>{filter.fieldCode} {filter.operator} {String(filter.value)}<button type="button" aria-label={translateCurrentLiteral('移除条件')} onClick={() => removeRowFilter(index)}>×</button></span>)}</div> : null}
            <div className="flex items-center gap-2">
              <label className="text-xs text-slate-500" htmlFor="data-browser-sort">{translateCurrentLiteral('服务端排序')}</label>
              <select id="data-browser-sort" className="form-input h-8 text-xs" value={rowSort?.fieldCode ?? ''} onChange={(event) => setRowSort(event.target.value ? { fieldCode: event.target.value, direction: rowSort?.direction ?? 'asc' } : null)}>
                <option value="">{translateCurrentLiteral('不排序')}</option>
                {rows?.fields.map((field) => <option key={field.fieldCode} value={field.fieldCode}>{field.fieldName || field.fieldCode}</option>)}
              </select>
              <select className="form-input h-8 text-xs" disabled={!rowSort} value={rowSort?.direction ?? 'asc'} onChange={(event) => setRowSort((current) => current ? { ...current, direction: event.target.value as 'asc' | 'desc' } : current)}>
                <option value="asc">ASC</option>
                <option value="desc">DESC</option>
              </select>
            </div>
            <WorkbenchDataGrid
              editable={Boolean(rows?.editable)}
              editDisabledReason={rows?.editDisabledReason}
              fields={rows?.fields ?? []}
              primaryKeys={rows?.primaryKeys ?? []}
              rows={rows?.rows ?? []}
              savingCell={savingCell}
              onDelete={deleteRow}
              onEdit={openEditRow}
              onExport={() => void exportAllRows()}
              onCellCommit={commitCell}
            />
            <div className="flex justify-end gap-2">
              <button className="secondary-button h-7 text-xs" disabled={pageIndex <= 1} type="button" onClick={() => setPageIndex((value) => Math.max(1, value - 1))}>{translateCurrentLiteral("上一页")}</button>
              <span className="flex h-7 items-center text-xs text-slate-500">{translateCurrentLiteral('第')} {pageIndex} {translateCurrentLiteral('页')} / {translateCurrentLiteral('共')} {rows?.total ?? 0} {translateCurrentLiteral('行')}</span>
              <button className="secondary-button h-7 text-xs" disabled={!rows || pageIndex * rows.pageSize >= rows.total} type="button" onClick={() => setPageIndex((value) => value + 1)}>{translateCurrentLiteral("下一页")}</button>
            </div>
          </>
        ) : (
          <EmptyState text={translateCurrentLiteral('请选择左侧数据表')} />
        )}
      </div>
      <WorkbenchDrawerForm
        footer={<><button className="secondary-button" type="button" onClick={() => { setTableForm(null); setTablePlan(null); }}>{translateCurrentLiteral("取消")}</button>{tablePlan ? <button className="primary-button" disabled={deployTablePlanMutation.isPending} type="button" onClick={() => deployTablePlanMutation.mutate(tablePlan)}>{translateCurrentLiteral('确认风险并部署 DDL')}</button> : <button className="primary-button" disabled={!tableForm || createTablePlanMutation.isPending} type="button" onClick={() => tableForm && createTablePlanMutation.mutate(tableForm)}>{translateCurrentLiteral('生成 DDL 计划')}</button>}</>}
        open={Boolean(tableForm)}
        description={translateCurrentLiteral('定义表名、字段、主键和备注，保存后会在当前数据库真实执行 DDL。')}
        title={translateCurrentLiteral("创建数据表")}
        width="xl"
        onClose={() => setTableForm(null)}
      >
        {tableForm ? <>{renderTableForm()}{tablePlan ? <SchemaChangePlanPanel plan={tablePlan} /> : null}</> : null}
      </WorkbenchDrawerForm>
      <WorkbenchDrawerForm
        footer={<><button className="secondary-button" type="button" onClick={() => { setAlterTableForm(null); setAlterTablePlan(null); }}>{translateCurrentLiteral('Cancel')}</button>{alterTablePlan ? <button className="primary-button" disabled={deployAlterTablePlanMutation.isPending} type="button" onClick={() => deployAlterTablePlanMutation.mutate(alterTablePlan)}>{translateCurrentLiteral('Confirm risk and deploy DDL')}</button> : <button className="primary-button" disabled={!alterTableForm || createAlterTablePlanMutation.isPending} type="button" onClick={() => alterTableForm && createAlterTablePlanMutation.mutate(alterTableForm)}>{translateCurrentLiteral('Generate DDL plan')}</button>}</>}
        open={Boolean(alterTableForm)}
        description={translateCurrentLiteral('Edit the desired columns. The backend rereads current metadata and rejects stale plans before executing provider SQL.')}
        title={translateCurrentLiteral('Edit table schema')}
        width="xl"
        onClose={() => { setAlterTableForm(null); setAlterTablePlan(null); }}
      >
        {alterTableForm ? <>{renderAlterTableForm()}{alterTablePlan ? <SchemaChangePlanPanel plan={alterTablePlan} /> : null}</> : null}
      </WorkbenchDrawerForm>
      <WorkbenchDrawerForm
        footer={<><button className="secondary-button" type="button" onClick={() => setRowForm(null)}>{translateCurrentLiteral("取消")}</button><button className="primary-button" disabled={!rowForm || insertRowMutation.isPending || updateRowMutation.isPending} type="button" onClick={saveRow}>{rowForm?.originalRow ? translateCurrentLiteral('保存修改') : translateCurrentLiteral('保存')}</button></>}
        open={Boolean(rowForm)}
        description={rowForm?.originalRow ? translateCurrentLiteral('编辑当前主键定位的行数据，保存后直接写入当前数据库。') : translateCurrentLiteral('新增一行真实数据，字段值会按当前表结构提交。')}
        title={rowForm?.originalRow ? translateCurrentLiteral('编辑行数据') : translateCurrentLiteral('新增行数据')}
        width="md"
        onClose={() => setRowForm(null)}
      >
        {rowForm ? renderRowForm() : null}
      </WorkbenchDrawerForm>
    </section>
  );

  function openCreateTable() {
    setTableForm({
      alias: '',
      columns: [{ ...defaultColumn(), columnName: 'id', dataType: 'TEXT', nullable: false, primaryKey: true }, defaultColumn()],
      remark: '',
      schemaName: '',
      tableName: ''
    });
  }

  function renderTableForm() {
    if (!tableForm) return null;
    return (
      <div className="space-y-3">
        <Field label={translateCurrentLiteral('表名')} value={tableForm.tableName} onChange={(value) => setTableForm({ ...tableForm, tableName: value })} />
        <Field label={translateCurrentLiteral('别名')} value={tableForm.alias ?? ''} onChange={(value) => setTableForm({ ...tableForm, alias: value })} />
        <Field label="Schema" value={tableForm.schemaName ?? ''} onChange={(value) => setTableForm({ ...tableForm, schemaName: value })} />
        <textarea className="form-input min-h-20" placeholder={translateCurrentLiteral("备注")} value={tableForm.remark ?? ''} onChange={(event) => setTableForm({ ...tableForm, remark: event.target.value })} />
        <div className="space-y-2">
          <div className="flex items-center justify-between"><span className="text-sm font-medium text-slate-700">{translateCurrentLiteral("字段列表")}</span><button className="secondary-button h-8" type="button" onClick={() => setTableForm({ ...tableForm, columns: [...tableForm.columns, defaultColumn()] })}>{translateCurrentLiteral("添加字段")}</button></div>
          {tableForm.columns.map((column, index) => (
            <div className="grid gap-2 rounded-md border border-slate-200 p-2 md:grid-cols-[1fr_120px_80px_80px_64px]" key={index}>
              <input className="form-input h-8" placeholder={translateCurrentLiteral("字段名")} value={column.columnName} onChange={(event) => updateColumn(index, { columnName: event.target.value })} />
              <input className="form-input h-8" placeholder={translateCurrentLiteral("类型")} value={column.dataType} onChange={(event) => updateColumn(index, { dataType: event.target.value })} />
              <label className="flex items-center gap-1 text-sm"><input checked={column.nullable} type="checkbox" onChange={(event) => updateColumn(index, { nullable: event.target.checked })} />{translateCurrentLiteral("可空")}</label>
              <label className="flex items-center gap-1 text-sm"><input checked={column.primaryKey} type="checkbox" onChange={(event) => updateColumn(index, { primaryKey: event.target.checked })} />{translateCurrentLiteral("主键")}</label>
              <button className="danger-button h-8" type="button" onClick={() => setTableForm({ ...tableForm, columns: tableForm.columns.filter((_, current) => current !== index) })}>{translateCurrentLiteral("删除")}</button>
            </div>
          ))}
        </div>
      </div>
    );
  }

  function updateColumn(index: number, patch: Partial<ApplicationDataSourceCreateTableColumnRequest>) {
    if (!tableForm) return;
    setTableForm({ ...tableForm, columns: tableForm.columns.map((column, current) => current === index ? { ...column, ...patch } : column) });
  }

  function openInsertRow() {
    const values = Object.fromEntries((detailQuery.data?.data.columns ?? []).map((column) => [column.columnName, '']));
    setRowForm({ values });
  }

  function openEditRow(row: Record<string, unknown>) {
    setRowForm({ originalRow: { ...row }, values: { ...row } });
  }

  function commitCell(row: Record<string, unknown>, fieldCode: string, value: unknown) {
    if (!rows?.primaryKeys.length) {
      message.error(translateCurrentLiteral('当前表没有主键，无法定位行数据'));
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
        onError: (error, request) => handleRowMutationError(error, 'update', request),
        onSettled: () => setSavingCell(null)
      }
    );
  }

  function renderRowForm() {
    if (!rowForm) return null;
    return <WorkbenchRowDataForm columns={detailQuery.data?.data.columns ?? []} mode={rowForm.originalRow ? 'edit' : 'create'} primaryKeys={rows?.primaryKeys ?? []} values={rowForm.values} onChange={(values) => setRowForm({ ...rowForm, values })} />;
  }

  function saveRow() {
    if (!rowForm) return;
    if (rowForm.originalRow) {
      const request = buildTableRowUpdateRequest(rowForm.originalRow, rowForm.values, {
        concurrencyColumn: rows?.concurrencyColumn,
        primaryKeys: rows?.primaryKeys ?? []
      });
      updateRowMutation.mutate(request, { onError: (error, failedRequest) => handleRowMutationError(error, 'update', failedRequest) });
    } else {
      insertRowMutation.mutate(buildTableRowInsertRequest(rowForm.values));
    }
  }

  function deleteRow(row: Record<string, unknown>) {
    confirm({
      content: translateCurrentLiteral('确认删除当前行数据？'),
      confirmText: translateCurrentLiteral('删除'),
      title: translateCurrentLiteral('删除行数据'),
      onConfirm: async () => {
        const deleted = await executeDeleteRow(buildTableRowDeleteRequest(row, {
          concurrencyColumn: rows?.concurrencyColumn,
          primaryKeys: rows?.primaryKeys ?? []
        }));
        if (!deleted) return;
        message.success(translateCurrentLiteral('行数据已删除'));
        await rowsQuery.refetch();
      }
    });
  }

  function handleRowMutationError(error: unknown, operation: RowConflict['operation'], request: RowConflict['request']) {
    const response = isHttpError(error) && error.status === 409 ? readRowConflictPayload(error.data) : null;
    if (response) {
      setRowConflict({ operation, request, response });
      return;
    }
    message.error(getErrorMessage(error, 'Row operation failed'));
  }

  function openAlterTable() {
    const detail = detailQuery.data?.data;
    if (!detail || isView(detail.table)) return;
    setAlterTableForm({
      tableName: detail.table.tableName,
      schemaName: detail.table.schemaName,
      columns: detail.columns.map((column) => ({
        columnName: column.columnName,
        dataType: column.dataType,
        defaultValue: '',
        nullable: column.nullable,
        primaryKey: column.primaryKey,
        remark: ''
      }))
    });
    setAlterTablePlan(null);
  }

  function renderAlterTableForm() {
    if (!alterTableForm) return null;
    return (
      <div className="space-y-3">
        <div className="rounded border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-600">{alterTableForm.schemaName ? `${alterTableForm.schemaName}.${alterTableForm.tableName}` : alterTableForm.tableName}</div>
        <div className="flex items-center justify-between"><span className="text-sm font-medium text-slate-700">{translateCurrentLiteral('Desired columns')}</span><button className="secondary-button h-8" type="button" onClick={() => setAlterTableForm({ ...alterTableForm, columns: [...alterTableForm.columns, defaultColumn()] })}>{translateCurrentLiteral('Add column')}</button></div>
        {alterTableForm.columns.map((column, index) => (
          <div className="grid gap-2 rounded-md border border-slate-200 p-2 md:grid-cols-[1fr_120px_80px_80px_64px]" key={`${column.columnName}-${index}`}>
            <input className="form-input h-8" placeholder={translateCurrentLiteral('Column name')} value={column.columnName} onChange={(event) => updateAlterColumn(index, { columnName: event.target.value })} />
            <input className="form-input h-8" placeholder={translateCurrentLiteral('Type')} value={column.dataType} onChange={(event) => updateAlterColumn(index, { dataType: event.target.value })} />
            <label className="flex items-center gap-1 text-sm"><input checked={column.nullable} type="checkbox" onChange={(event) => updateAlterColumn(index, { nullable: event.target.checked })} />{translateCurrentLiteral('Nullable')}</label>
            <label className="flex items-center gap-1 text-sm"><input checked={column.primaryKey} type="checkbox" onChange={(event) => updateAlterColumn(index, { primaryKey: event.target.checked })} />{translateCurrentLiteral('Primary key')}</label>
            <button className="danger-button h-8" type="button" onClick={() => setAlterTableForm({ ...alterTableForm, columns: alterTableForm.columns.filter((_, current) => current !== index) })}>{translateCurrentLiteral('Remove')}</button>
          </div>
        ))}
      </div>
    );
  }

  function updateAlterColumn(index: number, patch: Partial<ApplicationDataSourceCreateTableColumnRequest>) {
    if (!alterTableForm) return;
    setAlterTableForm({ ...alterTableForm, columns: alterTableForm.columns.map((column, current) => current === index ? { ...column, ...patch } : column) });
  }

  async function exportAllRows() {
    try {
      const request: ApplicationDataSourceTableRowsExportRequest = {
        filters: rowFilters,
        keyword: rowKeyword,
        maxRows: 100_000,
        sorts: rowSort ? [rowSort] : []
      };
      const response = await streamApplicationDataSourceTableRowsExport(dataSourceId, selectedTable, request);
      const url = URL.createObjectURL(response.blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = response.fileName;
      anchor.click();
      URL.revokeObjectURL(url);
      message.success(translateCurrentLiteral('Export request completed. The streamed file is limited by the server maximum row policy.'));
    } catch (error) {
      message.error(getErrorMessage(error, translateCurrentLiteral('导出失败')));
    }
  }

  function addRowFilter() {
    const value = filterValue.trim();
    if (!filterField || !value) return;
    setPageIndex(1);
    setRowFilters((current) => [...current, { fieldCode: filterField, operator: filterOperator, value }]);
    setFilterValue('');
  }

  function removeRowFilter(index: number) {
    setPageIndex(1);
    setRowFilters((current) => current.filter((_, currentIndex) => currentIndex !== index));
  }

  async function executeDeleteRow(request: ApplicationDataSourceTableRowDeleteRequest) {
    try {
      await deleteApplicationDataSourceTableRow(dataSourceId, selectedTable, request);
      return true;
    } catch (error) {
      handleRowMutationError(error, 'delete', request);
      return false;
    }
  }

  async function overwriteRowConflict(conflict: RowConflict) {
    try {
      const request = { ...conflict.request, conflictResolution: 'overwrite' as const };
      if (conflict.operation === 'update') {
        await updateApplicationDataSourceTableRow(dataSourceId, selectedTable, request as ApplicationDataSourceTableRowUpsertRequest);
      } else {
        await deleteApplicationDataSourceTableRow(dataSourceId, selectedTable, request as ApplicationDataSourceTableRowDeleteRequest);
      }
      setRowConflict(null);
      setRowForm(null);
      message.success('Conflict overwritten with local values');
      await rowsQuery.refetch();
    } catch (error) {
      handleRowMutationError(error, conflict.operation, conflict.request);
    }
  }

  async function retryRowConflict(conflict: RowConflict) {
    if (conflict.operation === 'update') {
      if (conflict.response.serverValues && conflict.response.localValues) {
        setRowForm({ originalRow: conflict.response.serverValues, values: conflict.response.localValues });
        setRowConflict(null);
      }
      return;
    }

    if (!conflict.response.serverValues) return;
    try {
      await deleteApplicationDataSourceTableRow(dataSourceId, selectedTable, {
        ...conflict.request,
        conflictResolution: 'retry',
        originalValues: conflict.response.serverValues
      } as ApplicationDataSourceTableRowDeleteRequest);
      setRowConflict(null);
      message.success('Delete retried with the latest server values');
      await rowsQuery.refetch();
    } catch (error) {
      handleRowMutationError(error, conflict.operation, conflict.request);
    }
  }

  function renderTreeItem(table: ApplicationDataSourceTable, icon: ReactNode) {
    const name = qualifiedTableName(table);
    const selected = selectedTable === name;
    return (
      <div className="group flex items-center gap-1" key={name}>
        <button
          className={[
            'flex h-7 min-w-0 flex-1 items-center gap-1.5 rounded px-2 text-left text-xs transition',
            selected ? 'bg-primary-50 font-semibold text-primary-700' : 'text-slate-700 hover:bg-slate-50 hover:text-slate-950'
          ].join(' ')}
          type="button"
          onClick={() => selectObject(name)}
        >
          <span className="shrink-0">{icon}</span>
          <span className="min-w-0 flex-1 truncate">{table.tableName}</span>
        </button>
        <button aria-label={`${favoriteNames.has(name) ? translateCurrentLiteral('取消收藏') : translateCurrentLiteral('收藏')} ${table.tableName}`} className="rounded p-1 text-slate-400 hover:text-amber-500" type="button" title={favoriteNames.has(name) ? translateCurrentLiteral('取消收藏') : translateCurrentLiteral('收藏')} onClick={() => toggleFavorite(name)}>
          <Star className="h-3.5 w-3.5" fill={favoriteNames.has(name) ? 'currentColor' : 'none'} />
        </button>
        <button aria-label={`${translateCurrentLiteral('刷新对象')} ${table.tableName}`} className="rounded p-1 text-slate-400 hover:text-primary-600" type="button" title={translateCurrentLiteral('刷新对象')} onClick={() => refreshNodeMutation.mutate(table)}>
          <RefreshCw className="h-3.5 w-3.5" />
        </button>
      </div>
    );
  }

  function selectObject(name: string) {
    onSelectedTableChange(name);
    setOpenObjects((current) => current.includes(name) ? current : [...current, name]);
    setRecentNames((current) => {
      const next = [name, ...current.filter((item) => item !== name)].slice(0, 12);
      writeObjectNames(dataSourceId, 'recent', next);
      return next;
    });
  }

  function closeObject(name: string) {
    setOpenObjects((current) => {
      const next = current.filter((item) => item !== name);
      if (selectedTable === name) {
        onSelectedTableChange(next.at(-1) ?? '');
      }
      return next;
    });
  }

  function toggleFavorite(name: string) {
    setFavoriteNames((current) => {
      const next = new Set(current);
      if (next.has(name)) next.delete(name); else next.add(name);
      writeObjectNames(dataSourceId, 'favorites', [...next]);
      return next;
    });
  }

  function toggleSchema(schemaName: string) {
    setCollapsedSchemas((current) => {
      const next = new Set(current);
      if (next.has(schemaName)) {
        next.delete(schemaName);
      } else {
        next.add(schemaName);
      }
      return next;
    });
  }

  function toggleGroup(groupKey: string) {
    setCollapsedGroups((current) => {
      const next = new Set(current);
      if (next.has(groupKey)) {
        next.delete(groupKey);
      } else {
        next.add(groupKey);
      }
      return next;
    });
  }
}

function RowConflictPanel({ conflict, onDismiss, onOverwrite, onRetry }: { conflict: RowConflict; onDismiss: () => void; onOverwrite: () => void; onRetry: () => void }) {
  const canRetry = conflict.response.canRetry && conflict.response.serverValues && conflict.response.localValues;
  return (
    <section className="rounded-md border border-red-300 bg-red-50 p-3 text-sm text-red-950">
      <div className="font-semibold">Concurrent change detected</div>
      <div className="mt-1 text-xs leading-5">{conflict.response.conflictMessage ?? 'The row changed on the server before this operation completed.'}</div>
      <div className="mt-2 grid gap-2 md:grid-cols-2">
        <div><div className="mb-1 text-xs font-semibold">Server values</div><pre className="max-h-32 overflow-auto rounded bg-white p-2 text-[11px]">{JSON.stringify(conflict.response.serverValues ?? {}, null, 2)}</pre></div>
        <div><div className="mb-1 text-xs font-semibold">Local values</div><pre className="max-h-32 overflow-auto rounded bg-white p-2 text-[11px]">{JSON.stringify(conflict.response.localValues ?? {}, null, 2)}</pre></div>
      </div>
      <div className="mt-2 flex flex-wrap gap-2">
        <button className="secondary-button h-8 text-xs" type="button" onClick={onDismiss}>Dismiss</button>
        <button className="secondary-button h-8 text-xs" disabled={!canRetry} type="button" onClick={onRetry}>Reload server and retry</button>
        <button className="danger-button h-8 text-xs" disabled={!conflict.response.canOverwrite} type="button" onClick={onOverwrite}>Overwrite server</button>
      </div>
    </section>
  );
}

function SchemaChangePlanPanel({ plan }: { plan: ApplicationDataSourceSchemaChangePlanResponse }) {
  return (
    <section className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-950">
      <div className="font-semibold">{translateCurrentLiteral('DDL 风险确认')}</div>
      <div className="mt-1 text-xs leading-5">{plan.target} · {plan.provider} · {translateCurrentLiteral('风险等级')} {plan.riskLevel}</div>
      <ul className="mt-2 list-disc space-y-1 pl-5 text-xs">
        {plan.risks.map((risk) => <li key={risk}>{risk}</li>)}
      </ul>
      <pre className="mt-2 max-h-36 overflow-auto rounded bg-slate-950 p-2 text-[11px] leading-5 text-slate-100">{plan.sqlPreview}</pre>
      <div className={`mt-2 text-xs ${plan.reversible ? 'text-emerald-700' : 'text-red-700'}`}>
        {plan.reversible ? translateCurrentLiteral('此计划标记为可回滚。') : translateCurrentLiteral('此计划不可自动回滚，部署前请确认已备份并准备人工恢复。')}
      </div>
    </section>
  );
}

function buildTableTree(tables: ApplicationDataSourceTable[]) {
  const schemaMap = new Map<string, { name: string; tables: ApplicationDataSourceTable[]; views: ApplicationDataSourceTable[] }>();

  for (const table of tables) {
    const schemaName = table.schemaName || 'main';
    const schema = schemaMap.get(schemaName) ?? { name: schemaName, tables: [], views: [] };
    if (isView(table)) {
      schema.views.push(table);
    } else {
      schema.tables.push(table);
    }
    schemaMap.set(schemaName, schema);
  }

  return Array.from(schemaMap.values()).map((schema) => ({
    ...schema,
    tables: schema.tables.sort(compareTableName),
    views: schema.views.sort(compareTableName)
  }));
}

function compareTableName(left: ApplicationDataSourceTable, right: ApplicationDataSourceTable) {
  return left.tableName.localeCompare(right.tableName);
}

function isView(table: ApplicationDataSourceTable) {
  return table.tableType.toLowerCase().includes('view');
}

function TreeGroup({ children, collapsed, count, icon, onToggle, title }: { children: ReactNode; collapsed: boolean; count: number; icon: ReactNode; onToggle: () => void; title: string }) {
  return (
    <div className="ml-4">
      <button className="flex h-6 w-full items-center gap-1.5 rounded px-2 text-[11px] font-semibold uppercase tracking-wide text-slate-500 transition hover:bg-slate-50" type="button" onClick={onToggle}>
        {collapsed ? <ChevronRight className="h-3 w-3 text-slate-400" /> : <ChevronDown className="h-3 w-3 text-slate-400" />}
        {icon}
        <span>{title}</span>
        <span className="rounded-full bg-slate-100 px-1.5 py-0.5 text-[11px] font-medium text-slate-500">{count}</span>
      </button>
      {collapsed ? null : <div className="ml-4 border-l border-slate-100 pl-1.5">{count > 0 ? children : <div className="px-2 py-1 text-xs text-slate-400">{translateCurrentLiteral("暂无对象")}</div>}</div>}
    </div>
  );
}

function Field({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return (
    <label className="block text-xs font-medium text-slate-700">
      {label}
      <input className="form-input mt-1 h-8" value={value} onChange={(event) => onChange(event.target.value)} />
    </label>
  );
}

function EmptyState({ text }: { text: string }) {
  return <div className="rounded-md border border-dashed border-slate-200 px-4 py-10 text-center text-sm text-slate-500">{text}</div>;
}

type RowConflict = {
  operation: 'delete' | 'update';
  request: ApplicationDataSourceTableRowDeleteRequest | ApplicationDataSourceTableRowUpsertRequest;
  response: ApplicationDataSourceTableRowMutationResponse;
};

function storageKey(dataSourceId: string, kind: 'favorites' | 'recent') {
  return `astererp:data-center:object-explorer:${dataSourceId}:${kind}`;
}

function readObjectNames(dataSourceId: string, kind: 'favorites' | 'recent'): Set<string> {
  return new Set(readObjectNameList(dataSourceId, kind));
}

function readObjectNameList(dataSourceId: string, kind: 'favorites' | 'recent'): string[] {
  try {
    const value = JSON.parse(localStorage.getItem(storageKey(dataSourceId, kind)) ?? '[]');
    return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : [];
  } catch {
    return [];
  }
}

function readRecentNames(dataSourceId: string): string[] {
  return readObjectNameList(dataSourceId, 'recent');
}

function writeObjectNames(dataSourceId: string, kind: 'favorites' | 'recent', names: string[]) {
  localStorage.setItem(storageKey(dataSourceId, kind), JSON.stringify(names));
}
