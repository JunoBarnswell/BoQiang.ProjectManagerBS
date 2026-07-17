import type {
  DataTableColumn,
  DataTableCondition,
  DataTableFilterType,
  DataTableOperator,
  DataTableQueryState,
  DataTableSortRule
} from './tableTypes';

export type QueryFieldType = DataTableFilterType;
export type QueryState = DataTableQueryState;

export function readInitialQueryState(
  conditions?: DataTableCondition[] | QueryState | undefined,
  fallback?: QueryState
): QueryState {
  if (!conditions && !fallback) {
    return { conditions: [], matchMode: 'and' };
  }

  const source = conditions ?? fallback;
  if (Array.isArray(source)) {
    return {
      conditions: source,
      matchMode: 'and'
    };
  }

  return {
    conditions: source?.conditions ?? [],
    matchMode: source?.matchMode === 'or' ? 'or' : 'and'
  };
}

export function safeClamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

export function valueToString(value: unknown): string {
  if (typeof value === 'number' || typeof value === 'boolean') {
    return String(value);
  }

  if (value === null || value === undefined) {
    return '';
  }

  return String(value);
}

export function normalizeNumber(value: unknown): number | null {
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : null;
  }

  if (value instanceof Date) {
    const time = value.getTime();
    return Number.isFinite(time) ? time : null;
  }

  if (value === null || value === undefined) {
    return null;
  }

  const parsed = Number.parseFloat(String(value).trim());
  return Number.isFinite(parsed) ? parsed : null;
}

export function normalizeDate(value: unknown): number | null {
  if (value instanceof Date) {
    const time = value.getTime();
    return Number.isFinite(time) ? time : null;
  }

  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }

  if (value === null || value === undefined) {
    return null;
  }

  const time = Date.parse(String(value).trim());
  return Number.isFinite(time) ? time : null;
}

export function inferFieldType(value: unknown): QueryFieldType {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return 'number';
  }

  if (value instanceof Date) {
    return 'date';
  }

  if (typeof value === 'string') {
    const time = Date.parse(value.trim());
    if (Number.isFinite(time)) {
      return 'date';
    }
  }

  return 'text';
}

export function extractFieldValue(row: unknown, key: string): unknown {
  if (row === null || row === undefined) {
    return undefined;
  }

  const segments = key.split('.');
  let cursor: unknown = row;

  for (const segment of segments) {
    if (!segment) {
      continue;
    }

    if (typeof cursor !== 'object' || cursor === null) {
      return undefined;
    }

    cursor = (cursor as Record<string, unknown>)[segment];
  }

  return cursor;
}

export function isQueryValueReady(value: unknown): boolean {
  if (typeof value === 'boolean') {
    return true;
  }

  if (typeof value === 'number') {
    return Number.isFinite(value);
  }

  return valueToString(value).trim().length > 0;
}

export function isBetweenReady(from: unknown, to: unknown): boolean {
  return isQueryValueReady(from) && isQueryValueReady(to);
}

export function compareConditionValues(
  left: unknown,
  right: unknown,
  operator: DataTableOperator,
  fieldType?: QueryFieldType
): boolean {
  if (operator === 'contains') {
    return valueToString(left).toLowerCase().includes(valueToString(right).trim().toLowerCase());
  }

  if (operator === 'startsWith') {
    return valueToString(left).toLowerCase().startsWith(valueToString(right).trim().toLowerCase());
  }

  if (operator === 'endsWith') {
    return valueToString(left).toLowerCase().endsWith(valueToString(right).trim().toLowerCase());
  }

  if (fieldType === 'number') {
    const leftNumber = normalizeNumber(left);
    const rightNumber = normalizeNumber(right);
    if (leftNumber === null || rightNumber === null) {
      return false;
    }

    if (operator === 'equals') return leftNumber === rightNumber;
    if (operator === 'notEquals') return leftNumber !== rightNumber;
    if (operator === 'gt') return leftNumber > rightNumber;
    if (operator === 'gte') return leftNumber >= rightNumber;
    if (operator === 'lt') return leftNumber < rightNumber;
    if (operator === 'lte') return leftNumber <= rightNumber;
    return false;
  }

  if (fieldType === 'date') {
    const leftDate = normalizeDate(left);
    const rightDate = normalizeDate(right);
    if (leftDate === null || rightDate === null) {
      return false;
    }

    if (operator === 'equals') return leftDate === rightDate;
    if (operator === 'notEquals') return leftDate !== rightDate;
    if (operator === 'gt') return leftDate > rightDate;
    if (operator === 'gte') return leftDate >= rightDate;
    if (operator === 'lt') return leftDate < rightDate;
    if (operator === 'lte') return leftDate <= rightDate;
    return false;
  }

  if (operator === 'equals') {
    return valueToString(left) === valueToString(right);
  }

  if (operator === 'notEquals') {
    return valueToString(left) !== valueToString(right);
  }

  return false;
}

export function compareBetweenValues(
  left: unknown,
  from: unknown,
  to: unknown,
  fieldType: QueryFieldType
): boolean {
  if (!isBetweenReady(from, to)) {
    return false;
  }

  if (fieldType === 'number') {
    const leftNumber = normalizeNumber(left);
    const fromNumber = normalizeNumber(from);
    const toNumber = normalizeNumber(to);
    if (leftNumber === null || fromNumber === null || toNumber === null) {
      return false;
    }

    const lower = Math.min(fromNumber, toNumber);
    const upper = Math.max(fromNumber, toNumber);
    return leftNumber >= lower && leftNumber <= upper;
  }

  if (fieldType === 'date') {
    const leftDate = normalizeDate(left);
    const fromDate = normalizeDate(from);
    const toDate = normalizeDate(to);
    if (leftDate === null || fromDate === null || toDate === null) {
      return false;
    }

    const lower = Math.min(fromDate, toDate);
    const upper = Math.max(fromDate, toDate);
    return leftDate >= lower && leftDate <= upper;
  }

  const leftText = valueToString(left);
  const fromText = valueToString(from);
  const toText = valueToString(to);
  return leftText >= fromText && leftText <= toText;
}

export function matchesCondition<TItem>(
  row: TItem,
  condition: DataTableCondition,
  fieldTypeMap: Map<string, QueryFieldType>
): boolean {
  if (!condition.field) {
    return true;
  }

  const fieldType = fieldTypeMap.get(condition.field) ?? 'text';
  const fieldValue = extractFieldValue(row as unknown, condition.field);

  if (condition.operator === 'between') {
    return compareBetweenValues(fieldValue, condition.value, condition.valueTo, fieldType);
  }

  if (!isQueryValueReady(condition.value)) {
    return true;
  }

  return compareConditionValues(fieldValue, condition.value, condition.operator, fieldType);
}

export function evaluateQueryRows<TItem>(
  rows: TItem[],
  queryState: QueryState,
  fieldTypeMap: Map<string, QueryFieldType>
): TItem[] {
  if (queryState.conditions.length === 0) {
    return rows;
  }

  const usableConditions = queryState.conditions.filter((condition) => {
    if (!condition.field) {
      return false;
    }

    if (condition.operator === 'between') {
      return isBetweenReady(condition.value, condition.valueTo);
    }

    return isQueryValueReady(condition.value);
  });

  if (usableConditions.length === 0) {
    return rows;
  }

  return rows.filter((row) => {
    const checks = usableConditions.map((condition) => matchesCondition(row, condition, fieldTypeMap));
    return queryState.matchMode === 'or' ? checks.some(Boolean) : checks.every(Boolean);
  });
}

export function compareValues(left: unknown, right: unknown): number {
  if (left === right) {
    return 0;
  }

  if (left === null || left === undefined) {
    return -1;
  }

  if (right === null || right === undefined) {
    return 1;
  }

  if (typeof left === 'number' && typeof right === 'number') {
    return left - right;
  }

  return valueToString(left).localeCompare(valueToString(right), undefined, { numeric: true, sensitivity: 'base' });
}

export function normalizeNumberOrText(value: unknown): string | number | boolean | null {
  if (typeof value === 'number' || typeof value === 'boolean' || value === null) {
    return value;
  }

  return String(value ?? '');
}

export function inferQueryFieldTypeFromRows<TItem>(rows: TItem[], fieldKey: string): QueryFieldType {
  for (const row of rows) {
    const value = extractFieldValue(row as unknown, fieldKey);
    if (value === null || value === undefined) {
      continue;
    }

    return inferFieldType(value);
  }

  return 'text';
}

export function sortRows<TItem>(
  rows: TItem[],
  sortRules: DataTableSortRule[],
  getSortValue: (row: TItem, rule: DataTableSortRule) => unknown
): TItem[] {
  if (sortRules.length === 0) {
    return rows;
  }

  return [...rows].sort((left, right) => {
    for (const rule of sortRules) {
      const compared = compareValues(getSortValue(left, rule), getSortValue(right, rule));
      if (compared !== 0) {
        return rule.direction === 'asc' ? compared : -compared;
      }
    }

    return 0;
  });
}

export function buildFieldTypeMap<TItem>(
  rows: TItem[],
  fields: Array<{ key: string; type?: DataTableFilterType }>
): Map<string, QueryFieldType> {
  return new Map(fields.map((field) => [field.key, field.type ?? inferQueryFieldTypeFromRows(rows, field.key)]));
}

export function inferColumnFieldType<TItem>(
  rows: TItem[],
  column: Pick<DataTableColumn<TItem>, 'filterType' | 'key'>
): QueryFieldType {
  return column.filterType ?? inferQueryFieldTypeFromRows(rows, column.key);
}
