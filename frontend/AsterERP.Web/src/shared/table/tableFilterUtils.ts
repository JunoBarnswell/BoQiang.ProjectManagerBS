import type { QueryFieldType } from './tableQueryUtils';
import type {
  DataTableColumn,
  DataTableFilterType,
  DataTableOperator
} from './tableTypes';

export const OPERATOR_OPTIONS: Array<{ labelKey: string; value: DataTableOperator }> = [
  { labelKey: 'table.operator.contains', value: 'contains' },
  { labelKey: 'table.operator.equals', value: 'equals' },
  { labelKey: 'table.operator.notEquals', value: 'notEquals' },
  { labelKey: 'table.operator.startsWith', value: 'startsWith' },
  { labelKey: 'table.operator.endsWith', value: 'endsWith' },
  { labelKey: 'table.operator.gt', value: 'gt' },
  { labelKey: 'table.operator.gte', value: 'gte' },
  { labelKey: 'table.operator.lt', value: 'lt' },
  { labelKey: 'table.operator.lte', value: 'lte' },
  { labelKey: 'table.operator.between', value: 'between' }
];

const FILTER_OPERATORS_BY_TYPE: Record<DataTableFilterType, DataTableOperator[]> = {
  boolean: ['equals', 'notEquals'],
  date: ['equals', 'gt', 'gte', 'lt', 'lte', 'between'],
  number: ['equals', 'notEquals', 'gt', 'gte', 'lt', 'lte', 'between'],
  select: ['equals', 'notEquals'],
  text: ['contains', 'equals', 'notEquals', 'startsWith', 'endsWith']
};

export function getColumnSortField<TItem>(column: DataTableColumn<TItem>): string {
  return (column.sortField ?? column.key).trim();
}

export function getColumnFilterField<TItem>(column: DataTableColumn<TItem>): string {
  return (column.filterField ?? column.binding ?? column.key).trim();
}

export function isColumnSortable<TItem>(column: DataTableColumn<TItem>, isRemoteSorting: boolean): boolean {
  if (column.key === 'rowIndex' || getColumnSortField(column).length === 0) {
    return false;
  }

  return isRemoteSorting ? column.sortable === true : column.sortable !== false;
}

export function isColumnFilterable<TItem>(column: DataTableColumn<TItem>): boolean {
  return column.key !== 'rowIndex' && column.filterable === true && getColumnFilterField(column).length > 0;
}

export function getColumnFilterType<TItem>(
  column: DataTableColumn<TItem>,
  fieldTypeMap: Map<string, QueryFieldType>
): DataTableFilterType {
  return column.filterType ?? fieldTypeMap.get(getColumnFilterField(column)) ?? 'text';
}

export function getColumnFilterOperators<TItem>(column: DataTableColumn<TItem>, filterType: DataTableFilterType): DataTableOperator[] {
  return column.filterOperators?.length ? column.filterOperators : FILTER_OPERATORS_BY_TYPE[filterType];
}

export function getDefaultFilterOperator<TItem>(column: DataTableColumn<TItem>, filterType: DataTableFilterType): DataTableOperator {
  return getColumnFilterOperators(column, filterType)[0] ?? 'equals';
}
