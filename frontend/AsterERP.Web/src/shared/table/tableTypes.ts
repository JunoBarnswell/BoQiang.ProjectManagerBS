import type { CSSProperties, ReactNode } from 'react';

import type { BreakpointName } from '../../core/responsive/breakpoint';

export type DataTableSide = 'left' | 'right';
export type DataTableOperator =
  | 'contains'
  | 'equals'
  | 'notEquals'
  | 'startsWith'
  | 'endsWith'
  | 'gt'
  | 'gte'
  | 'lt'
  | 'lte'
  | 'between';

export type DataTableFilterType = 'boolean' | 'date' | 'number' | 'select' | 'text';

export interface DataTableFilterOption {
  label: ReactNode;
  value: string | number | boolean;
}

export interface DataTableColumn<TItem> {
  align?: 'left' | 'center' | 'right';
  canFreeze?: boolean;
  canHide?: boolean;
  children?: DataTableColumn<TItem>[];
  fixed?: DataTableSide;
  frozen?: DataTableSide;
  hideBelow?: BreakpointName;
  isVisible?: boolean;
  binding?: string;
  key: string;
  order?: number;
  filterable?: boolean;
  filterField?: string;
  filterType?: DataTableFilterType;
  filterOptions?: DataTableFilterOption[];
  filterOperators?: DataTableOperator[];
  responsivePriority?: number;
  sortable?: boolean;
  sortField?: string;
  title: ReactNode;
  width?: string;
  render?: (item: TItem, index: number) => ReactNode;
  cellClassName?: string | ((item: TItem, column: DataTableColumn<TItem>, index: number) => string);
}

export interface DataTableCellSpan {
  colSpan: number;
  columnKey: string;
  rowIndex: number;
  rowSpan: number;
}

export interface DataTableCondition {
  field: string;
  operator: DataTableOperator;
  value: string | number | boolean | null;
  valueTo?: string | number | boolean | null;
}

export type DataTableSortDirection = 'asc' | 'desc';

export interface DataTableSortRule {
  direction: DataTableSortDirection;
  field: string;
}

export type DataTableSortState = DataTableSortRule[];

export interface DataTableQueryState {
  conditions: DataTableCondition[];
  matchMode: 'and' | 'or';
}

export interface DataTablePaginationState {
  current: number;
  pageSize: number;
  total: number;
}

export interface DataTableSelectionState {
  onChange: (selectedRowKeys: string[]) => void;
  selectedRowKeys: string[];
}

export interface DataTableRowReorderEvent<TItem> {
  orderedRows: TItem[];
  row: TItem;
  rows: TItem[];
  sourceIndex: number;
  sourceKey: string;
  targetIndex: number;
  targetKey: string;
}

export interface DataTableRowReorderConfig<TItem> {
  disabled?: boolean;
  enabled?: boolean;
  onReorder?: (event: DataTableRowReorderEvent<TItem>) => Promise<void> | void;
}

export interface DataTableColumnSetting {
  binding?: string;
  children?: DataTableColumnSetting[];
  width?: string;
  key: string;
  fixed?: DataTableSide;
  isVisible?: boolean;
  merge?: {
    direction: string;
    enabled: boolean;
    fields: string[];
    strategy: string;
  };
  order?: number;
  queryField?: string;
  renderer?: string;
  sortField?: string;
  title?: string;
  valueSource?: {
    field?: string | null;
    fields?: string[] | null;
    path?: string | null;
    template?: string | null;
    type: string;
  };
}

export interface DataTableQueryFormField {
  key: string;
  label: ReactNode;
  type?: DataTableFilterType;
  options?: DataTableFilterOption[];
  placeholder?: string;
}

export interface DataTableProps<TItem> {
  className?: string;
  columns: DataTableColumn<TItem>[];
  cellSpans?: DataTableCellSpan[];
  columnSettings?: DataTableColumnSetting[];
  columnSettingsActions?: ReactNode;
  columnSettingsKey?: string;
  conditions?: DataTableCondition[] | DataTableQueryState;
  emptyText?: ReactNode;
  fitScreen?: boolean;
  loading?: boolean;
  onPageChange?: (nextPage: number) => void;
  onSortChange?: (sortKey: string, direction: DataTableSortDirection | null) => void;
  onPageSizeChange?: (pageSize: number) => void;
  onQueryChange?: (query: DataTableQueryState) => void;
  onColumnSettingsChange?: (columns: DataTableColumnSetting[]) => void;
  onRefreshColumns?: (columns: DataTableColumnSetting[]) => void;
  onRow?: (item: TItem, index: number) => void;
  onRowDoubleClick?: (item: TItem, index: number) => void;
  pageSizeOptions?: number[];
  pagination?: DataTablePaginationState;
  rowKey: (item: TItem, index: number) => string;
  rowActions?: (item: TItem, index: number) => ReactNode;
  rowReorder?: DataTableRowReorderConfig<TItem>;
  rowStyle?: CSSProperties;
  rowClassName?: string | ((item: TItem, index: number) => string);
  rowVirtualize?: boolean;
  rowVirtualization?: {
    overscan?: number;
    rowHeight?: number;
  };
  showColumnSettings?: boolean;
  showQueryBuilder?: boolean;
  rows: TItem[];
  searchFields?: DataTableQueryFormField[];
  selection?: DataTableSelectionState;
  selectionColumnTitle?: ReactNode;
  sorts?: DataTableSortRule[];
  onSortsChange?: (sorts: DataTableSortRule[]) => void;
  style?: CSSProperties;
  tableQuery?: DataTableQueryState;
  toolbar?: ReactNode;
}
