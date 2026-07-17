import type { ReactNode } from 'react';

import type { DataTableColumn, DataTableSide } from './tableTypes';

export interface RenderColumn<TItem> {
  column: DataTableColumn<TItem>;
  fixed?: DataTableSide;
  isVisible: boolean;
  order: number;
  widthPx: number;
}

export interface HeaderCell<TItem> {
  column: DataTableColumn<TItem>;
  colSpan: number;
  isLeaf: boolean;
  rowSpan: number;
}

export function maxHeaderDepth<TItem>(columns: DataTableColumn<TItem>[]): number {
  if (columns.length === 0) {
    return 1;
  }

  return Math.max(...columns.map((column) => column.children?.length ? 1 + maxHeaderDepth(column.children) : 1));
}

export function buildHeaderRows<TItem>(
  columns: DataTableColumn<TItem>[],
  runtimeMap: Map<string, RenderColumn<TItem>>,
  depth: number,
  maxDepth: number,
  rows: HeaderCell<TItem>[][] = []
): HeaderCell<TItem>[][] {
  rows[depth - 1] ??= [];

  for (const column of columns) {
    if (column.children?.length) {
      const childRows = buildHeaderRows(column.children, runtimeMap, depth + 1, maxDepth, rows);
      const colSpan = countVisibleLeafColumns(column.children, runtimeMap);
      if (colSpan > 0) {
        childRows[depth - 1].push({ column, colSpan, isLeaf: false, rowSpan: 1 });
      }
      continue;
    }

    if (runtimeMap.has(column.key)) {
      rows[depth - 1].push({ column, colSpan: 1, isLeaf: true, rowSpan: maxDepth - depth + 1 });
    }
  }

  return rows;
}

export function countVisibleLeafColumns<TItem>(
  columns: DataTableColumn<TItem>[],
  runtimeMap: Map<string, RenderColumn<TItem>>
): number {
  return columns.reduce((count, column) => {
    if (column.children?.length) {
      return count + countVisibleLeafColumns(column.children, runtimeMap);
    }

    return count + (runtimeMap.has(column.key) ? 1 : 0);
  }, 0);
}

export function getColumnRuntimeOrder<TItem>(
  column: DataTableColumn<TItem>,
  runtimeMap: Map<string, RenderColumn<TItem>>
): number {
  if (column.children?.length) {
    const childOrders = column.children
      .map((child) => getColumnRuntimeOrder(child, runtimeMap))
      .filter((order) => Number.isFinite(order));
    return childOrders.length > 0 ? Math.min(...childOrders) : Number.MAX_SAFE_INTEGER;
  }

  return runtimeMap.get(column.key)?.order ?? Number.MAX_SAFE_INTEGER;
}

export function sortHeaderColumns<TItem>(
  columns: DataTableColumn<TItem>[],
  runtimeMap: Map<string, RenderColumn<TItem>>
): DataTableColumn<TItem>[] {
  return columns
    .map((column) => ({
      ...column,
      children: column.children?.length ? sortHeaderColumns(column.children, runtimeMap) : column.children
    }))
    .filter((column) => countVisibleLeafColumns([column], runtimeMap) > 0)
    .sort((left, right) => getColumnRuntimeOrder(left, runtimeMap) - getColumnRuntimeOrder(right, runtimeMap));
}

export function titleToText(title: ReactNode, fallback: string): string {
  if (typeof title === 'string' || typeof title === 'number') {
    return String(title);
  }

  return fallback;
}
