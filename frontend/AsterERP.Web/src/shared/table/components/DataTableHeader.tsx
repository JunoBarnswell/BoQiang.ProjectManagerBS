import { Funnel } from 'lucide-react';
import type { CSSProperties, ReactNode } from 'react';

import { formatMessage } from '../../../core/i18n/formatMessage';
import { translateCurrentLiteral, useI18n } from '../../../core/i18n/I18nProvider';
import {
  getColumnSortField,
  isColumnFilterable,
  isColumnSortable
} from '../tableFilterUtils';
import { titleToText, type HeaderCell, type RenderColumn } from '../tableLayoutUtils';
import type { DataTableColumn, DataTableCondition, DataTableSortRule } from '../tableTypes';

interface DataTableHeaderProps<TItem> {
  activeHeaderFilterKey: string | null;
  getCellStyle: (column: RenderColumn<TItem>, isHeader?: boolean) => CSSProperties;
  getStickyClass: (column: RenderColumn<TItem>) => string;
  hasAnyActions: boolean;
  headerCheckbox: ReactNode;
  headerRows: HeaderCell<TItem>[][];
  isRemoteSorting: boolean;
  onCloseHeaderFilter: () => void;
  onOpenHeaderFilter: (column: DataTableColumn<TItem>) => void;
  renderHeaderFilter: (column: DataTableColumn<TItem>) => ReactNode;
  rowReorderHeader: ReactNode;
  sortRules: DataTableSortRule[];
  toggleSort: (field: string) => void;
  visibleColumnMap: Map<string, RenderColumn<TItem>>;
  findHeaderFilterCondition: (column: DataTableColumn<TItem>) => DataTableCondition | null;
}

export function DataTableHeader<TItem>({
  activeHeaderFilterKey,
  getCellStyle,
  getStickyClass,
  hasAnyActions,
  headerCheckbox,
  headerRows,
  isRemoteSorting,
  onCloseHeaderFilter,
  onOpenHeaderFilter,
  renderHeaderFilter,
  rowReorderHeader,
  sortRules,
  toggleSort,
  visibleColumnMap,
  findHeaderFilterCondition
}: DataTableHeaderProps<TItem>) {
  const { translate } = useI18n();
  return (
    <thead>
      {headerRows.map((headerRow, headerRowIndex) => (
        <tr key={`header-${headerRowIndex}`}>
          {headerRowIndex === 0 ? rowReorderHeader : null}
          {headerRowIndex === 0 ? headerCheckbox : null}
          {headerRow.map((cell) => {
            const runtimeColumn = visibleColumnMap.get(cell.column.key);
            const sortField = getColumnSortField(cell.column);
            const sortable = cell.isLeaf && isColumnSortable(cell.column, isRemoteSorting);
            const filterable = cell.isLeaf && isColumnFilterable(cell.column);
            const filterActive = filterable && findHeaderFilterCondition(cell.column) !== null;
            const sortIndex = sortRules.findIndex((rule) => rule.field === sortField);
            const sortRule = sortIndex >= 0 ? sortRules[sortIndex] : null;
            const headerStyle: CSSProperties = runtimeColumn
              ? getCellStyle(runtimeColumn, true)
              : { position: 'sticky', top: 0, zIndex: 3, background: 'var(--app-table-header-bg)' };
            return (
              <th
                className={['data-table__th text-left sticky top-0 z-[3] whitespace-nowrap relative', runtimeColumn ? getStickyClass(runtimeColumn) : ''].join(' ')}
                colSpan={cell.colSpan}
                key={`${headerRowIndex}-${cell.column.key}`}
                rowSpan={cell.rowSpan}
                style={headerStyle}
              >
                <div className="data-table-header-cell">
                  {sortable ? (
                    <button
                      className="data-table-header-cell__sort"
                      type="button"
                      onClick={() => toggleSort(sortField)}
                    >
                      <span>{typeof cell.column.title === 'string' ? translateCurrentLiteral(cell.column.title) : cell.column.title}</span>
                      {sortRule ? (
                        <span className="data-table-sort-indicator">
                          {sortRule.direction === 'asc' ? '↑' : '↓'}
                          {sortRules.length > 1 ? <small>{sortIndex + 1}</small> : null}
                        </span>
                      ) : null}
                    </button>
                  ) : (
                    <span className="data-table-header-cell__title">{typeof cell.column.title === 'string' ? translateCurrentLiteral(cell.column.title) : cell.column.title}</span>
                  )}
                  {filterable ? (
                    <button
                      aria-label={formatMessage(translate('table.filterFor'), { name: titleToText(cell.column.title, cell.column.key) })}
                      className={['data-table-header-cell__filter', filterActive ? 'data-table-header-cell__filter--active' : ''].filter(Boolean).join(' ')}
                      title={translate('table.filter')}
                      type="button"
                      onClick={(event) => {
                        event.stopPropagation();
                        if (activeHeaderFilterKey === cell.column.key) {
                          onCloseHeaderFilter();
                          return;
                        }
                        onOpenHeaderFilter(cell.column);
                      }}
                    >
                      <Funnel size={13} />
                    </button>
                  ) : null}
                  {renderHeaderFilter(cell.column)}
                </div>
              </th>
            );
          })}
          {headerRowIndex === 0 && hasAnyActions ? (
            <th
              className="data-table__th sticky right-0 top-0 z-[4] w-[110px] text-left shadow-[-4px_0_0_-2px_var(--app-border)]"
              rowSpan={headerRows.length}
              style={{ background: 'var(--app-table-header-bg)' }}
            >
              {translate('table.actions')}
            </th>
          ) : null}
        </tr>
      ))}
    </thead>
  );
}
