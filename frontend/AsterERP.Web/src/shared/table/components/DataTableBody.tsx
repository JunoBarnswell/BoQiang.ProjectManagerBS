import { GripVertical } from 'lucide-react';
import type { CSSProperties, ReactNode } from 'react';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { type RenderColumn } from '../tableLayoutUtils';
import type { DataTableCellSpan, DataTableProps, DataTableSelectionState } from '../tableTypes';

import { DataTableCellContent } from './DataTableCellContent';

interface RowReorderDragApi {
  draggingKey: string | null;
  dragOverKey: string | null;
  getDragSourceProps: (key: string) => Record<string, unknown>;
  getDropTargetProps: (key: string) => Record<string, unknown>;
}

interface DataTableBodyProps<TItem> {
  cellSpanMap: Map<string, DataTableCellSpan>;
  getCellStyle: (column: RenderColumn<TItem>, isHeader?: boolean) => CSSProperties;
  getCellValue: (row: TItem, column: RenderColumn<TItem>, index: number) => ReactNode;
  getStickyClass: (column: RenderColumn<TItem>) => string;
  hasAnyActions: boolean;
  onRow?: (item: TItem, index: number) => void;
  onRowDoubleClick?: (item: TItem, index: number) => void;
  pageOffset: number;
  renderRows: (renderRow: (row: TItem, index: number, style?: CSSProperties) => ReactNode) => ReactNode;
  rowActions?: DataTableProps<TItem>['rowActions'];
  rowClassName?: DataTableProps<TItem>['rowClassName'];
  rowKey: DataTableProps<TItem>['rowKey'];
  rowReorderDrag: RowReorderDragApi;
  rowReorderEnabled: boolean;
  rowStyle?: CSSProperties;
  selectedSet: Set<string>;
  selection?: DataTableSelectionState;
  shouldVirtualize: boolean;
  totalVirtualHeight: number;
  visibleColumns: RenderColumn<TItem>[];
}

export function DataTableBody<TItem>({
  cellSpanMap,
  getCellStyle,
  getCellValue,
  getStickyClass,
  hasAnyActions,
  onRow,
  onRowDoubleClick,
  pageOffset,
  renderRows,
  rowActions,
  rowClassName,
  rowKey,
  rowReorderDrag,
  rowReorderEnabled,
  rowStyle,
  selectedSet,
  selection,
  shouldVirtualize,
  totalVirtualHeight,
  visibleColumns
}: DataTableBodyProps<TItem>) {
  const { translate } = useI18n();
  return (
    <tbody
      className="relative"
      style={shouldVirtualize ? { position: 'relative', height: `${totalVirtualHeight}px` } : undefined}
    >
      {renderRows((row, rowIndex, virtualStyle) => {
        const rowId = rowKey(row, rowIndex);
        const isSelected = selectedSet.has(rowId);
        const rowDropProps = rowReorderEnabled ? rowReorderDrag.getDropTargetProps(rowId) : {};
        const rowClass = typeof rowClassName === 'function' ? rowClassName(row, rowIndex + pageOffset) : rowClassName;

        return (
          <tr
            className={[
              'transition-colors duration-200 hover:bg-[color-mix(in_srgb,var(--app-accent-soft)_56%,transparent)]',
              isSelected ? 'bg-[color-mix(in_srgb,var(--app-accent-soft)_28%,transparent)]' : '',
              rowReorderDrag.dragOverKey === rowId ? 'data-table__row--drag-over' : '',
              rowReorderDrag.draggingKey === rowId ? 'data-table__row--dragging' : '',
              rowClass
            ].filter(Boolean).join(' ')}
            key={rowId}
            onClick={() => onRow?.(row, rowIndex)}
            onDoubleClick={() => onRowDoubleClick?.(row, rowIndex)}
            style={{
              ...rowStyle,
              ...virtualStyle
            }}
            {...rowDropProps}
          >
            {rowReorderEnabled ? (
              <td className="data-table__td data-table__td--row-reorder">
                <button
                  aria-label={translate('table.dragSort')}
                  className="data-table-row-drag-handle"
                  type="button"
                  title={translate('table.dragSort')}
                  {...rowReorderDrag.getDragSourceProps(rowId)}
                  onClick={(event) => event.stopPropagation()}
                >
                  <GripVertical size={14} />
                </button>
              </td>
            ) : null}

            {selection ? (
              <td className="data-table__td data-table__td--selection">
                <input
                  checked={isSelected}
                  type="checkbox"
                  onChange={(event) => {
                    event.stopPropagation();
                    const next = new Set(selection.selectedRowKeys);
                    if (next.has(rowId)) {
                      next.delete(rowId);
                    } else {
                      next.add(rowId);
                    }
                    selection.onChange(Array.from(next));
                  }}
                />
              </td>
            ) : null}

            {visibleColumns.map((column) => {
              const cellSpan = cellSpanMap.get(`${rowIndex - pageOffset}:${column.column.key}`);
              if (cellSpan && (cellSpan.rowSpan <= 0 || cellSpan.colSpan <= 0)) {
                return null;
              }
              const cellClass = typeof column.column.cellClassName === 'function'
                ? column.column.cellClassName(row, column.column, rowIndex + pageOffset)
                : column.column.cellClassName;
              return (
                <td
                  className={['data-table__td text-left whitespace-nowrap', getStickyClass(column), cellClass].filter(Boolean).join(' ')}
                  colSpan={cellSpan?.colSpan}
                  key={`${rowId}-${column.column.key}`}
                  rowSpan={cellSpan?.rowSpan}
                  style={getCellStyle(column)}
                >
                  <DataTableCellContent>
                    {getCellValue(row, column, rowIndex)}
                  </DataTableCellContent>
                </td>
              );
            })}

            {hasAnyActions ? (
              <td className="data-table__td w-[110px] sticky right-0 z-[2] shadow-[-4px_0_0_-2px_var(--app-border)]" style={{ background: 'var(--app-table-cell-bg)' }} onClick={(event) => event.stopPropagation()} onDoubleClick={(event) => event.stopPropagation()}>
                {rowActions?.(row, rowIndex)}
              </td>
            ) : null}
          </tr>
        );
      })}
    </tbody>
  );
}
