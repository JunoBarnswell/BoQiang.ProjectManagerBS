import { TableSortLabel } from '@mui/material';
import type { ReactNode } from 'react';

export interface FlowListTableColumn<TItem> {
  key: string;
  render?: (item: TItem) => ReactNode;
  sortable?: boolean;
  title: ReactNode;
  width?: string;
}

interface FlowListTableProps<TItem> {
  columns: FlowListTableColumn<TItem>[];
  emptyText: ReactNode;
  getRowKey: (item: TItem) => string;
  loading?: boolean;
  order?: 'asc' | 'desc';
  orderBy?: string;
  rowActions?: (item: TItem) => ReactNode;
  rows: TItem[];
  onSort?: (key: string) => void;
}

export function FlowListTable<TItem>({ columns, emptyText, getRowKey, loading, order, orderBy, rowActions, rows, onSort }: FlowListTableProps<TItem>) {
  const gridTemplateColumns = `${columns.map((column) => column.width ?? 'minmax(120px, 1fr)').join(' ')}${rowActions ? ' minmax(120px, auto)' : ''}`;

  if (loading) {
    return <div aria-busy="true" className="flowise-native-loading" />;
  }

  if (rows.length === 0) {
    return <div className="flowise-native-empty">{emptyText}</div>;
  }

  return (
    <div className="flowise-native-table" role="table">
      <div className="flowise-native-table__head" style={{ gridTemplateColumns }} role="row">
        {columns.map((column) => (
          <span key={column.key} role="columnheader">
            {column.sortable ? (
              <TableSortLabel
                active={orderBy === column.key}
                className="flowise-native-table__sort"
                direction={orderBy === column.key ? order ?? 'asc' : 'asc'}
                onClick={() => onSort?.(column.key)}
              >
                {column.title}
              </TableSortLabel>
            ) : column.title}
          </span>
        ))}
        {rowActions ? <span role="columnheader" /> : null}
      </div>
      {rows.map((item) => (
        <div className="flowise-native-table__row" key={getRowKey(item)} style={{ gridTemplateColumns }} role="row">
          {columns.map((column) => (
            <span key={column.key} role="cell">
              {column.render ? column.render(item) : String((item as Record<string, unknown>)[column.key] ?? '')}
            </span>
          ))}
          {rowActions ? <span className="flowise-native-table__actions" role="cell">{rowActions(item)}</span> : null}
        </div>
      ))}
    </div>
  );
}
