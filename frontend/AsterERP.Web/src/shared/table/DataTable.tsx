import { useVirtualizer } from '@tanstack/react-virtual';
import { type CSSProperties, type ElementRef, type ReactNode, useEffect, useMemo, useRef, useState } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';
import { AppIcon } from '../icons/AppIcon';

import { ColumnSettingsPanel } from './column-settings/ColumnSettingsPanel';
import { DataTableBody } from './components/DataTableBody';
import { DataTableHeader } from './components/DataTableHeader';
import { DataTableHeaderFilter } from './components/DataTableHeaderFilter';
import { DataTablePagination } from './components/DataTablePagination';
import { DataTableQueryPanel } from './components/DataTableQueryPanel';
import {
  flattenLeafColumns,
  loadPersistedDisplaySettings,
  safeDisplayStorageKey,
  savePersistedDisplaySettings,
  useColumnSettings
} from './hooks/useColumnSettings';
import { useDragReorder } from './hooks/useDragReorder';
import { useTableSortRules } from './hooks/useTableSortRules';
import {
  getColumnFilterField,
  getColumnFilterType,
  getColumnSortField,
  getDefaultFilterOperator
} from './tableFilterUtils';
import {
  buildHeaderRows,
  maxHeaderDepth,
  sortHeaderColumns,
  type RenderColumn
} from './tableLayoutUtils';
import {
  evaluateQueryRows,
  extractFieldValue,
  inferQueryFieldTypeFromRows,
  normalizeNumberOrText,
  readInitialQueryState,
  safeClamp,
  sortRows,
  valueToString,
  type QueryFieldType,
  type QueryState
} from './tableQueryUtils';
import {
  buildColumnSettingsPanelColumns,
  buildRuntimeColumns,
  DEFAULT_PAGE_SIZE_OPTIONS,
  getDataTableCellStyle,
  getDataTableStickyClass,
  isDataTableConditionReady,
  moveArrayItem,
  normalizeHeaderFilterCondition
} from './tableRuntimeUtils';
import type {
  DataTableColumn,
  DataTableCondition,
  DataTableOperator,
  DataTableProps,
  DataTableQueryFormField
} from './tableTypes';

export function DataTable<TItem>({
  cellSpans = [],
  className = '',
  columns,
  columnSettings: externalColumnSettings,
  columnSettingsActions,
  columnSettingsKey,
  conditions,
  emptyText,
  fitScreen,
  loading,
  onPageChange,
  onQueryChange,
  onRefreshColumns,
  onRow,
  onRowDoubleClick,
  onSortChange,
  onSortsChange,
  onPageSizeChange,
  onColumnSettingsChange,
  pageSizeOptions = DEFAULT_PAGE_SIZE_OPTIONS,
  pagination,
  rowActions,
  rowKey,
  rowReorder,
  rowStyle,
  rowClassName,
  rowVirtualize = false,
  rowVirtualization,
  rows,
  searchFields = [],
  selection,
  showColumnSettings,
  showQueryBuilder = false,
  sorts,
  style,
  tableQuery,
  toolbar
}: DataTableProps<TItem>) {
  const { translate } = useI18n();
  const displayStorageKey = safeDisplayStorageKey(columnSettingsKey);
  const resolvedShowColumnSettings = showColumnSettings ?? Boolean(columnSettingsKey);
  const queryStateFromProps = useMemo(() => readInitialQueryState(conditions ?? tableQuery, undefined), [conditions, tableQuery]);
  const [queryMatchMode, setQueryMatchMode] = useState<'and' | 'or'>(queryStateFromProps.matchMode);
  const [queryConditions, setQueryConditions] = useState<DataTableCondition[]>(queryStateFromProps.conditions);
  const [activeQueryState, setActiveQueryState] = useState<QueryState>(queryStateFromProps);

  const [showSettingsPanel, setShowSettingsPanel] = useState(false);
  const [showVerticalLines, setShowVerticalLines] = useState<boolean>(() => loadPersistedDisplaySettings(displayStorageKey).showVerticalLines === true);
  const [showQueryPanel, setShowQueryPanel] = useState(false);
  const [activeHeaderFilterKey, setActiveHeaderFilterKey] = useState<string | null>(null);
  const [headerFilterDraft, setHeaderFilterDraft] = useState<DataTableCondition | null>(null);
  const [localPageSize, setLocalPageSize] = useState<number>(pagination?.pageSize ?? pageSizeOptions[0] ?? 10);
  const [internalPage, setInternalPage] = useState<number>(1);
  const [showQueryPanelHint, setShowQueryPanelHint] = useState(false);
  const {
    columnSettings,
    reorderColumn,
    resetColumnSettings,
    updateColumnSetting
  } = useColumnSettings({
    columnSettingsKey,
    columns,
    externalColumnSettings,
    onColumnSettingsChange,
    onRefreshColumns
  });
  const {
    addSortRule,
    clearSortRules,
    moveSortRule,
    removeSortRule,
    setSortRuleDirection,
    setSortRuleField,
    sortRules,
    toggleSort
  } = useTableSortRules({
    onSortChange,
    onSortsChange,
    sorts
  });
  const isRemoteSorting = typeof onSortChange === 'function' || typeof onSortsChange === 'function';

  const listRef = useRef<ElementRef<'div'>>(null);
  const headerCheckboxRef = useRef<ElementRef<'input'>>(null);
  const didSortRulesMountRef = useRef(false);
  const onPageChangeRef = useRef(onPageChange);
  const isInternalPagination = pagination === undefined;

  const queryFieldDefinitions = useMemo<DataTableQueryFormField[]>(() => {
    if (searchFields.length > 0) {
      return searchFields;
    }

    return flattenLeafColumns(columns).map((column) => ({
      key: column.key,
      label: column.title,
      type: inferQueryFieldTypeFromRows(rows, column.key)
    }));
  }, [columns, rows, searchFields]);

  const queryFieldTypeMap = useMemo(() => {
    const map = new Map<string, QueryFieldType>();
    for (const field of queryFieldDefinitions) {
      map.set(field.key, field.type ?? 'text');
    }
    return map;
  }, [queryFieldDefinitions]);

  useEffect(() => {
    savePersistedDisplaySettings(displayStorageKey, { showVerticalLines });
  }, [displayStorageKey, showVerticalLines]);

  useEffect(() => {
    const nextQueryState = readInitialQueryState(conditions ?? tableQuery, undefined);
    setQueryMatchMode(nextQueryState.matchMode);
    setQueryConditions(nextQueryState.conditions);
    setActiveQueryState(nextQueryState);
  }, [conditions, tableQuery]);

  useEffect(() => {
    if (pagination?.pageSize && Number.isFinite(pagination.pageSize)) {
      setLocalPageSize(pagination.pageSize);
    }
  }, [pagination?.pageSize]);

  useEffect(() => {
    onPageChangeRef.current = onPageChange;
  }, [onPageChange]);

  const sortRulesSignature = useMemo(
    () => sortRules.map((rule) => `${rule.field}:${rule.direction}`).join('|'),
    [sortRules]
  );

  useEffect(() => {
    if (!didSortRulesMountRef.current) {
      didSortRulesMountRef.current = true;
      return;
    }

    if (isInternalPagination) {
      setInternalPage(1);
    }
    onPageChangeRef.current?.(1);
  }, [isInternalPagination, sortRulesSignature]);

  const columnsRuntime = useMemo<RenderColumn<TItem>[]>(() => buildRuntimeColumns(columns, columnSettings), [columns, columnSettings]);

  const visibleColumns = useMemo(() => columnsRuntime.filter((column) => column.isVisible), [columnsRuntime]);
  const visibleColumnMap = useMemo(() => new Map(visibleColumns.map((column) => [column.column.key, column])), [visibleColumns]);
  const headerColumns = useMemo(() => sortHeaderColumns(columns, visibleColumnMap), [columns, visibleColumnMap]);
  const headerRows = useMemo(() => buildHeaderRows(headerColumns, visibleColumnMap, 1, maxHeaderDepth(headerColumns)), [headerColumns, visibleColumnMap]);
  const cellSpanMap = useMemo(
    () => new Map(cellSpans.map((span) => [`${span.rowIndex}:${span.columnKey}`, span])),
    [cellSpans]
  );

  const filteredRows = useMemo(() => {
    if (typeof onQueryChange === 'function') {
      return rows;
    }

    return evaluateQueryRows(rows, activeQueryState, queryFieldTypeMap);
  }, [rows, onQueryChange, activeQueryState, queryFieldTypeMap]);

  const orderedRows = useMemo(() => {
    if (sortRules.length === 0 || typeof onSortChange === 'function' || typeof onSortsChange === 'function') {
      return filteredRows;
    }

    return sortRows(filteredRows, sortRules, (row, rule) => {
      const sortColumn = columnsRuntime.find((column) => column.column.key === rule.field || getColumnSortField(column.column) === rule.field);
      const sortBinding = sortColumn?.column.binding ?? rule.field;
      return extractFieldValue(row as unknown, sortBinding);
    });
  }, [columnsRuntime, filteredRows, onSortChange, onSortsChange, sortRules]);

  const pageSize = pagination?.pageSize ?? localPageSize;
  const pageTotal = pagination?.total ?? orderedRows.length;
  const totalPages = Math.max(1, Math.ceil(pageTotal / pageSize));

  const currentPage = isInternalPagination ? internalPage : pagination?.current ?? 1;
  const safeCurrentPage = safeClamp(currentPage, 1, totalPages);
  const pageOffset = isInternalPagination ? (safeCurrentPage - 1) * pageSize : 0;
  const pageRows = isInternalPagination ? orderedRows.slice(pageOffset, pageOffset + pageSize) : orderedRows;
  const shouldVirtualize = rowVirtualize || rowVirtualization !== undefined || pageRows.length >= 120;
  const rowHeight = rowVirtualization?.rowHeight ?? 56;

  const virtualizer = useVirtualizer({
    count: pageRows.length,
    getScrollElement: () => listRef.current,
    estimateSize: () => rowHeight,
    overscan: rowVirtualization?.overscan ?? 8
  });

  const virtualRows = shouldVirtualize ? virtualizer.getVirtualItems() : [];

  const leftOffsets = useMemo(() => {
    const offsetByKey = new Map<string, number>();
    let cursor = 0;

    visibleColumns
      .filter((column) => column.fixed === 'left')
      .forEach((column) => {
        offsetByKey.set(column.column.key, cursor);
        cursor += column.widthPx;
      });

    return offsetByKey;
  }, [visibleColumns]);

  const rightOffsets = useMemo(() => {
    const offsetByKey = new Map<string, number>();
    const rightColumns = visibleColumns.filter((column) => column.fixed === 'right');
    let cursor = 0;

    for (let index = rightColumns.length - 1; index >= 0; index -= 1) {
      const item = rightColumns[index];
      if (!item) {
        continue;
      }
      offsetByKey.set(item.column.key, cursor);
      cursor += item.widthPx;
    }

    return offsetByKey;
  }, [visibleColumns]);

  const selectedSet = useMemo(() => new Set(selection?.selectedRowKeys ?? []), [selection?.selectedRowKeys]);
  const headerAllKeys = useMemo(() => pageRows.map((row, index) => rowKey(row, index + pageOffset)), [pageRows, rowKey, pageOffset]);
  const headerAllChecked = headerAllKeys.length > 0 && headerAllKeys.every((key) => selectedSet.has(key));
  const headerIndeterminate = !headerAllChecked && headerAllKeys.some((key) => selectedSet.has(key));

  useEffect(() => {
    if (!headerCheckboxRef.current) {
      return;
    }
    headerCheckboxRef.current.indeterminate = headerIndeterminate;
  }, [headerIndeterminate]);

  useEffect(() => {
    if (!isInternalPagination) {
      return;
    }

    setInternalPage(1);
  }, [activeQueryState.conditions, activeQueryState.matchMode, isInternalPagination, rows]);

  useEffect(() => {
    if (!isInternalPagination) {
      return;
    }

    setInternalPage((current) => safeClamp(current, 1, totalPages));
  }, [isInternalPagination, totalPages]);

  const hasPagination = pagination !== undefined || totalPages > 1;

  const getCellStyle = (column: RenderColumn<TItem>, isHeader = false): CSSProperties =>
    getDataTableCellStyle(column, leftOffsets, rightOffsets, isHeader);

  const getStickyClass = getDataTableStickyClass;

  const goPage = (nextPage: number) => {
    const safePage = safeClamp(nextPage, 1, totalPages);
    if (isInternalPagination) {
      setInternalPage(safePage);
    }
    onPageChange?.(safePage);
  };

  const applyQuery = (nextQuery: QueryState) => {
    setActiveQueryState(nextQuery);
    onQueryChange?.(nextQuery);
    goPage(1);
  };

  const setQueryFromDraft = () => {
    const cleaned = queryConditions.filter((item) => {
      const fieldType = queryFieldTypeMap.get(item.field) ?? 'text';
      return isDataTableConditionReady(item, fieldType);
    });

    if (cleaned.length === 0) {
      setShowQueryPanelHint(true);
      return;
    }

    setShowQueryPanelHint(false);
    applyQuery({
      conditions: cleaned,
      matchMode: queryMatchMode
    });
  };

  const clearQuery = () => {
    setQueryConditions([]);
    applyQuery({
      conditions: [],
      matchMode: queryMatchMode
    });
  };

  const findHeaderFilterCondition = (column: DataTableColumn<TItem>) => {
    const field = getColumnFilterField(column);
    return activeQueryState.conditions.find((condition) => condition.field === field) ?? null;
  };

  const openHeaderFilter = (column: DataTableColumn<TItem>) => {
    const field = getColumnFilterField(column);
    const filterType = getColumnFilterType(column, queryFieldTypeMap);
    const existing = findHeaderFilterCondition(column);
    setHeaderFilterDraft(existing ?? {
      field,
      operator: getDefaultFilterOperator(column, filterType),
      value: filterType === 'boolean' ? true : ''
    });
    setActiveHeaderFilterKey(column.key);
  };

  const closeHeaderFilter = () => {
    setActiveHeaderFilterKey(null);
    setHeaderFilterDraft(null);
  };

  const updateHeaderFilterDraft = (patch: Partial<DataTableCondition>) => {
    setHeaderFilterDraft((current) => current ? { ...current, ...patch } : current);
  };

  const applyHeaderFilter = (column: DataTableColumn<TItem>) => {
    if (!headerFilterDraft) {
      return;
    }

    const field = getColumnFilterField(column);
    const filterType = getColumnFilterType(column, queryFieldTypeMap);
    const nextConditions = activeQueryState.conditions.filter((condition) => condition.field !== field);
    const nextCondition = normalizeHeaderFilterCondition(headerFilterDraft, field);

    if (isDataTableConditionReady(nextCondition, filterType)) {
      nextConditions.push(nextCondition);
    }

    applyQuery({
      conditions: nextConditions,
      matchMode: activeQueryState.matchMode
    });
    closeHeaderFilter();
  };

  const clearHeaderFilter = (column: DataTableColumn<TItem>) => {
    const field = getColumnFilterField(column);
    applyQuery({
      conditions: activeQueryState.conditions.filter((condition) => condition.field !== field),
      matchMode: activeQueryState.matchMode
    });
    closeHeaderFilter();
  };

  const addCondition = () => {
    const firstField = queryFieldDefinitions[0];
    if (!firstField) {
      return;
    }

    setQueryConditions((current) => [...current, { field: String(firstField.key), operator: 'equals', value: '' }]);
  };

  const setConditionField = (index: number, field: string) => {
    setQueryConditions((current) => current.map((item, currentIndex) => (currentIndex === index ? { ...item, field } : item)));
  };

  const setConditionOperator = (index: number, operator: DataTableOperator) => {
    setQueryConditions((current) => current.map((item, currentIndex) => (currentIndex === index ? { ...item, operator } : item)));
  };

  const setConditionValue = (index: number, value: string | number | boolean | null) => {
    setQueryConditions((current) =>
      current.map((item, currentIndex) =>
        currentIndex === index ? { ...item, value: normalizeNumberOrText(value) } : item
      )
    );
  };

  const setConditionRange = (index: number, value: string | number | boolean | null) => {
    setQueryConditions((current) =>
      current.map((item, currentIndex) =>
        currentIndex === index ? { ...item, valueTo: normalizeNumberOrText(value) } : item
      )
    );
  };

  const removeCondition = (index: number) => {
    setQueryConditions((current) => current.filter((_, currentIndex) => currentIndex !== index));
  };

  const rowReorderEnabled = rowReorder?.enabled === true && rowReorder.disabled !== true && typeof rowReorder.onReorder === 'function';
  const rowReorderDrag = useDragReorder({
    enabled: rowReorderEnabled,
    onDrop: async (sourceKey, targetKey) => {
      const keyedRows = pageRows.map((row, index) => ({
        index,
        key: rowKey(row, index + pageOffset),
        row
      }));
      const source = keyedRows.find((item) => item.key === sourceKey);
      const target = keyedRows.find((item) => item.key === targetKey);
      if (!source || !target) {
        return;
      }

      await rowReorder?.onReorder?.({
        orderedRows: moveArrayItem(pageRows, source.index, target.index),
        row: source.row,
        rows: pageRows,
        sourceIndex: source.index + pageOffset,
        sourceKey,
        targetIndex: target.index + pageOffset,
        targetKey
      });
    }
  });

  const getRowReorderHeader = (rowSpan = 1) => {
    if (!rowReorderEnabled) {
      return null;
    }

    return (
      <th
        className="data-table__th data-table__th--row-reorder text-left sticky top-0 z-[3] whitespace-nowrap"
        rowSpan={rowSpan}
        style={{ position: 'sticky', top: 0, zIndex: 3, background: '#f8fafc' }}
      />
    );
  };

  const getConditionInputNode = (condition: DataTableCondition, index: number) => {
    const selectedField = queryFieldDefinitions.find((candidate) => candidate.key === condition.field);
    const fieldType = selectedField?.type ?? 'text';

    if (condition.operator === 'between' && (fieldType === 'number' || fieldType === 'date')) {
      return (
        <div className={"inline-flex items-center gap-[8px]"}>
          <input
            className={"rounded-[8px] py-[0.38rem] px-[0.55rem] bg-[var(--app-card)] border border-[var(--app-border)]"}
            onChange={(event) => setConditionValue(index, event.target.value)}
            type={fieldType === 'number' ? 'number' : 'date'}
            value={valueToString(condition.value)}
          />
          <span className={"text-[var(--app-muted)] text-[0.84rem]"}>{translate('table.rangeTo')}</span>
          <input
            className={"rounded-[8px] py-[0.38rem] px-[0.55rem] bg-[var(--app-card)] border border-[var(--app-border)]"}
            onChange={(event) => setConditionRange(index, event.target.value)}
            type={fieldType === 'number' ? 'number' : 'date'}
            value={valueToString(condition.valueTo)}
          />
        </div>
      );
    }

    if (fieldType === 'select' && selectedField?.options?.length) {
      return (
        <select
          className={"rounded-[8px] py-[0.38rem] px-[0.55rem] bg-[var(--app-card)] border border-[var(--app-border)]"}
          onChange={(event) => setConditionValue(index, event.target.value)}
          value={valueToString(condition.value)}
        >
          <option value="">{translate('table.all')}</option>
          {selectedField.options.map((option) => (
            <option key={String(option.value)} value={String(option.value)}>
              {option.label}
            </option>
          ))}
        </select>
      );
    }

    return (
      <input
        className={"rounded-[8px] py-[0.38rem] px-[0.55rem] bg-[var(--app-card)] border border-[var(--app-border)]"}
        onChange={(event) => setConditionValue(index, event.target.value)}
        type={fieldType === 'number' ? 'number' : fieldType === 'date' ? 'date' : 'text'}
        value={valueToString(condition.value)}
      />
    );
  };

  const getHeaderCheckbox = (rowSpan = 1) => {
    if (!selection) {
      return null;
    }

    return (
      <th className="data-table__th data-table__th--selection sticky top-0 z-[3] whitespace-nowrap" rowSpan={rowSpan} style={{ position: 'sticky', top: 0, zIndex: 3, background: '#f8fafc' }}>
        <input
          checked={headerAllChecked}
          ref={headerCheckboxRef}
          type="checkbox"
          onChange={() => {
            const next = new Set(selection.selectedRowKeys);
            if (headerAllChecked) {
              headerAllKeys.forEach((key) => next.delete(key));
            } else {
              headerAllKeys.forEach((key) => next.add(key));
            }
            selection.onChange(Array.from(next));
          }}
        />
      </th>
    );
  };

  const getCellValue = (row: TItem, column: RenderColumn<TItem>, index: number) => {
    if (column.column.render) {
      return column.column.render(row, index);
    }

    return valueToString(extractFieldValue(row as unknown, column.column.binding ?? column.column.key));
  };

  const resetColumnSetting = () => {
    resetColumnSettings();
    setShowVerticalLines(false);
  };

  const columnSettingMap = useMemo(() => new Map(columnSettings.map((item) => [item.key, item])), [columnSettings]);
  const settingsPanelColumns = useMemo(
    () => buildColumnSettingsPanelColumns(columnsRuntime, columnSettingMap, isRemoteSorting),
    [columnSettingMap, columnsRuntime, isRemoteSorting]
  );

  const hasAnyActions = rowActions !== undefined;
  const shouldRenderEmpty = !loading && pageRows.length === 0;

  const renderRows = (renderRow: (row: TItem, index: number, style?: CSSProperties) => ReactNode) => {
    if (!shouldVirtualize) {
      return pageRows.map((row, index) => renderRow(row, index + pageOffset));
    }

    return virtualRows.map((virtualRow) => {
      const row = pageRows[virtualRow.index];
      if (!row) {
        return null;
      }
      return renderRow(row, virtualRow.index + pageOffset, {
        position: 'absolute',
        left: 0,
        right: 0,
        transform: `translateY(${virtualRow.start}px)`
      });
    });
  };

  const onPageSizeValueChange = (nextPageSize: number) => {
    if (!Number.isFinite(nextPageSize) || nextPageSize <= 0) {
      return;
    }

    setLocalPageSize(nextPageSize);
    if (isInternalPagination) {
      setInternalPage(1);
    }
    onPageSizeChange?.(nextPageSize);
    goPage(1);
  };

  return (
    <div className={['flex flex-col flex-1 h-full min-h-0', 'data-table-shell', className].filter(Boolean).join(' ')} style={style}>
      {toolbar ? <div className={"flex flex-wrap gap-[10px]"}>{toolbar}</div> : null}

      {(showQueryBuilder || resolvedShowColumnSettings) && (
        <div className={['flex gap-[10px] items-center flex-wrap', 'data-table-toolbar'].join(' ')}>
          {showQueryBuilder ? (
            <button className={`bg-white border border-gray-300 px-2.5 py-1 rounded text-xs transition-colors shadow-sm flex items-center gap-1 ${showQueryPanel ? 'text-primary-600 border-primary-300 bg-primary-50' : 'text-gray-700 hover:bg-gray-50'}`} type="button" onClick={() => setShowQueryPanel((current) => !current)}>
              <AppIcon name="funnel" /> {showQueryPanel ? translate('table.closeAdvancedQuery') : translate('table.advancedQuery')}
            </button>
          ) : null}
          {resolvedShowColumnSettings ? (
            <button className={`bg-white border border-gray-300 px-2.5 py-1 rounded text-xs transition-colors shadow-sm flex items-center gap-1 ${showSettingsPanel ? 'text-primary-600 border-primary-300 bg-primary-50' : 'text-gray-700 hover:bg-gray-50'}`} type="button" onClick={() => setShowSettingsPanel((current) => !current)}>
              <AppIcon name="gear" /> {showSettingsPanel ? translate('table.closeColumnSettings') : translate('table.columnSettings')}
            </button>
          ) : null}
        </div>
      )}

      {showQueryBuilder && showQueryPanel ? (
        <DataTableQueryPanel
          conditions={queryConditions}
          fields={queryFieldDefinitions}
          matchMode={queryMatchMode}
          renderConditionInput={getConditionInputNode}
          showHint={showQueryPanelHint}
          onAddCondition={addCondition}
          onApply={setQueryFromDraft}
          onClear={clearQuery}
          onConditionFieldChange={setConditionField}
          onConditionOperatorChange={setConditionOperator}
          onMatchModeChange={setQueryMatchMode}
          onRemoveCondition={removeCondition}
        />
      ) : null}

      {resolvedShowColumnSettings && showSettingsPanel ? (
        <ColumnSettingsPanel
          actions={columnSettingsActions}
          columns={settingsPanelColumns}
          onAddSortRule={addSortRule}
          onClearSortRules={clearSortRules}
          onClose={() => setShowSettingsPanel(false)}
          onColumnChange={updateColumnSetting}
          onColumnReorder={reorderColumn}
          onMoveSortRule={moveSortRule}
          onRemoveSortRule={removeSortRule}
          onReset={resetColumnSetting}
          onShowVerticalLinesChange={setShowVerticalLines}
          onSortRuleDirectionChange={setSortRuleDirection}
          onSortRuleFieldChange={setSortRuleField}
          showVerticalLines={showVerticalLines}
          sortRules={sortRules}
        />
      ) : null}

      <div className={["overflow-auto flex-1", 'data-table-wrapper'].filter(Boolean).join(' ')} style={fitScreen ? { minHeight: 0 } : undefined}>
        <div className={"overflow-auto min-h-0"} ref={listRef}>
          <table className={['w-full border-collapse table-fixed', 'data-table', showVerticalLines ? 'data-table--vertical-lines' : ''].filter(Boolean).join(' ')}>
            <DataTableHeader
              activeHeaderFilterKey={activeHeaderFilterKey}
              findHeaderFilterCondition={findHeaderFilterCondition}
              getCellStyle={getCellStyle}
              getStickyClass={getStickyClass}
              hasAnyActions={hasAnyActions}
              headerCheckbox={getHeaderCheckbox(headerRows.length)}
              headerRows={headerRows}
              isRemoteSorting={isRemoteSorting}
              renderHeaderFilter={(column) => (
                <DataTableHeaderFilter
                  activeColumnKey={activeHeaderFilterKey}
                  column={column}
                  draft={headerFilterDraft}
                  fieldTypeMap={queryFieldTypeMap}
                  onApply={applyHeaderFilter}
                  onClear={clearHeaderFilter}
                  onClose={closeHeaderFilter}
                  onDraftChange={updateHeaderFilterDraft}
                />
              )}
              rowReorderHeader={getRowReorderHeader(headerRows.length)}
              sortRules={sortRules}
              toggleSort={toggleSort}
              visibleColumnMap={visibleColumnMap}
              onCloseHeaderFilter={closeHeaderFilter}
              onOpenHeaderFilter={openHeaderFilter}
            />

            <DataTableBody
              cellSpanMap={cellSpanMap}
              getCellStyle={getCellStyle}
              getCellValue={getCellValue}
              getStickyClass={getStickyClass}
              hasAnyActions={hasAnyActions}
              pageOffset={pageOffset}
              renderRows={renderRows}
              rowActions={rowActions}
              rowClassName={rowClassName}
              rowKey={rowKey}
              rowReorderDrag={rowReorderDrag}
              rowReorderEnabled={rowReorderEnabled}
              rowStyle={rowStyle}
              selectedSet={selectedSet}
              selection={selection}
              shouldVirtualize={shouldVirtualize}
              totalVirtualHeight={virtualizer.getTotalSize()}
              visibleColumns={visibleColumns}
              onRow={onRow}
              onRowDoubleClick={onRowDoubleClick}
            />
          </table>
        </div>
      </div>

      {shouldRenderEmpty ? <div className="p-8 text-center text-gray-400 text-sm">{emptyText ?? translate('table.emptyDefault')}</div> : null}
      {loading ? <div className="p-8 text-center text-gray-400 text-sm flex items-center justify-center gap-2"><AppIcon className="animate-spin text-lg" name="spinner" /> {translate('table.loadingDefault')}</div> : null}

      {hasPagination ? (
        <DataTablePagination
          currentPage={safeCurrentPage}
          pageSize={pageSize}
          pageSizeOptions={pageSizeOptions}
          total={pageTotal}
          totalPages={totalPages}
          onPageChange={goPage}
          onPageSizeChange={onPageSizeChange ? onPageSizeValueChange : undefined}
        />
      ) : null}
    </div>
  );
}

export default DataTable;
