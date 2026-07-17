export interface SortQueryRule {
  direction?: string;
  field: string;
  order?: string;
}

export interface FilterQueryRule {
  field: string;
  operator: string;
  value: boolean | Date | number | string | null;
  valueTo?: boolean | Date | number | string | null;
}

export type QueryStringValue = boolean | Date | FilterQueryRule[] | number | SortQueryRule[] | string | null | undefined;
export type QueryStringParams = Record<string, QueryStringValue>;

export function buildQueryString<TParams extends object>(params: TParams): string {
  const searchParams = new URLSearchParams();

  Object.entries(params as QueryStringParams).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') {
      return;
    }

    if (Array.isArray(value)) {
      if (key === 'sorts') {
        appendSorts(searchParams, value as SortQueryRule[]);
      }

      if (key === 'filters') {
        appendFilters(searchParams, value as FilterQueryRule[]);
      }

      return;
    }

    searchParams.set(key, normalizeValue(value));
  });

  const query = searchParams.toString();
  return query ? `?${query}` : '';
}

function appendFilters(searchParams: URLSearchParams, filters: FilterQueryRule[]): void {
  let outputIndex = 0;
  filters.forEach((filter) => {
    const field = filter.field?.trim();
    const operator = normalizeFilterOperator(filter.operator);
    if (!field || !isUsableFilterValue(filter.value, operator) || (operator === 'between' && !isUsableFilterValue(filter.valueTo, operator))) {
      return;
    }

    const value = filter.value;
    if (value === null || value === undefined) {
      return;
    }

    searchParams.set(`filters[${outputIndex}].field`, field);
    searchParams.set(`filters[${outputIndex}].operator`, operator);
    searchParams.set(`filters[${outputIndex}].value`, normalizeValue(value));
    if (operator === 'between' && filter.valueTo !== undefined && filter.valueTo !== null && filter.valueTo !== '') {
      searchParams.set(`filters[${outputIndex}].valueTo`, normalizeValue(filter.valueTo));
    }
    outputIndex += 1;
  });
}

function appendSorts(searchParams: URLSearchParams, sorts: SortQueryRule[]): void {
  let outputIndex = 0;
  sorts.forEach((sort) => {
    const field = sort.field?.trim();
    if (!field) {
      return;
    }

    searchParams.set(`sorts[${outputIndex}].field`, field);
    searchParams.set(`sorts[${outputIndex}].order`, normalizeSortOrder(sort));
    outputIndex += 1;
  });
}

function normalizeSortOrder(sort: SortQueryRule): string {
  return sort.order?.trim() || sort.direction?.trim() || 'asc';
}

function normalizeFilterOperator(operator: string | undefined): string {
  return operator?.trim() || 'equals';
}

function isUsableFilterValue(value: FilterQueryRule['value'] | undefined, operator: string): boolean {
  if (typeof value === 'boolean') {
    return true;
  }

  if (typeof value === 'number') {
    return Number.isFinite(value);
  }

  if (value instanceof Date) {
    return Number.isFinite(value.getTime());
  }

  if (operator === 'between' && value === null) {
    return false;
  }

  return value !== undefined && value !== null && String(value).trim().length > 0;
}

function normalizeValue(value: Exclude<QueryStringValue, FilterQueryRule[] | SortQueryRule[] | null | undefined>): string {
  return value instanceof Date ? value.toISOString() : String(value);
}
