import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { httpClient } from '../../core/http/httpClient';
import type { DataTableCondition } from '../../shared/table/tableTypes';
import type { QueryViewQueryCondition, QueryViewQueryRequest, QueryViewQuerySort } from '../runtime/query-views.api';
import { queryQueryView } from '../runtime/query-views.api';
import type { GridPageResult, OptionItemDto } from '../shared.types';

import type { DictItemListItemDto, DictItemUpsertRequest, DictTypeListItemDto, DictTypeUpsertRequest } from './system.types';

type QueryViewRow = Record<string, unknown>;

export interface DictTypeQuery {
  filters?: DataTableCondition[];
  keyword?: string;
  pageIndex?: number;
  pageSize?: number;
  sorts?: QueryViewQuerySort[];
}

export interface DictItemQuery {
  filters?: DataTableCondition[];
  pageIndex?: number;
  pageSize?: number;
  sorts?: QueryViewQuerySort[];
}

function asString(value: unknown, fallback = ''): string {
  return value === undefined || value === null ? fallback : String(value);
}

function asNullableString(value: unknown): string | null {
  const text = asString(value).trim();
  return text ? text : null;
}

function asNumber(value: unknown, fallback = 0): number {
  const numberValue = Number(value);
  return Number.isFinite(numberValue) ? numberValue : fallback;
}

function asBoolean(value: unknown): boolean {
  if (typeof value === 'boolean') {
    return value;
  }

  if (typeof value === 'number') {
    return value !== 0;
  }

  const text = asString(value).toLowerCase();
  return text === 'true' || text === '1' || text === 'yes';
}

function optionalCondition(field: string, operator: QueryViewQueryCondition['operator'], value: unknown): QueryViewQueryCondition | null {
  if (value === undefined || value === null || value === '') {
    return null;
  }

  return { field, operator, value: value as boolean | number | string };
}

function queryViewRequest(
  pageIndex: number | undefined,
  pageSize: number | undefined,
  conditions: Array<QueryViewQueryCondition | null>,
  filters: DataTableCondition[] = [],
  sorts: QueryViewQueryRequest['sorts'] = []
): QueryViewQueryRequest {
  return {
    conditions: [
      ...conditions.filter((item): item is QueryViewQueryCondition => Boolean(item)),
      ...filters
        .filter((item) => item.field && item.value !== undefined && item.value !== null && item.value !== '')
        .map((item) => ({
          field: item.field,
          operator: item.operator,
          value: item.value as boolean | number | string,
          valueTo: item.valueTo
        }))
    ],
    pageIndex: pageIndex ?? 1,
    pageSize: pageSize ?? 20,
    sorts
  };
}

async function queryViewGrid<TItem>(
  viewCode: string,
  request: QueryViewQueryRequest,
  mapper: (row: QueryViewRow) => TItem,
  signal?: AbortSignal
): Promise<ApiEnvelope<GridPageResult<TItem>>> {
  const response = await queryQueryView(viewCode, request, signal);
  return {
    ...response,
    data: {
      items: response.data.rows.map(mapper),
      total: response.data.total
    }
  };
}

function mapDictType(row: QueryViewRow): DictTypeListItemDto {
  return {
    dictCode: asString(row.dictCode),
    dictName: asString(row.dictName),
    id: asString(row.id),
    isEnabled: asBoolean(row.isEnabled),
    remark: asNullableString(row.remark)
  };
}

function mapDictItem(row: QueryViewRow): DictItemListItemDto {
  return {
    dictTypeId: asString(row.dictTypeId),
    id: asString(row.id),
    isEnabled: asBoolean(row.isEnabled),
    itemLabel: asString(row.itemLabel),
    itemValue: asString(row.itemValue),
    remark: asNullableString(row.remark),
    sortOrder: asNumber(row.sortOrder)
  };
}

export function getDictTypes(query: DictTypeQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<DictTypeListItemDto>>> {
  return queryViewGrid(
    'system_dict_type',
    queryViewRequest(query.pageIndex, query.pageSize, [
      optionalCondition('__keyword', 'contains', query.keyword)
    ], query.filters ?? [], query.sorts ?? []),
    mapDictType,
    signal
  );
}

export function createDictType(request: DictTypeUpsertRequest): Promise<ApiEnvelope<DictTypeListItemDto>> {
  return httpClient.post<DictTypeListItemDto, DictTypeUpsertRequest>('/system/dicts/types', request);
}

export function getDictType(id: string): Promise<ApiEnvelope<DictTypeListItemDto>> {
  return httpClient.get<DictTypeListItemDto>(`/system/dicts/types/${id}`);
}

export function updateDictType(id: string, request: DictTypeUpsertRequest): Promise<ApiEnvelope<DictTypeListItemDto>> {
  return httpClient.put<DictTypeListItemDto, DictTypeUpsertRequest>(`/system/dicts/types/${id}`, request);
}

export function deleteDictType(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/system/dicts/types/${id}`);
}

export function getDictOptions(dictCode: string, signal?: AbortSignal): Promise<ApiEnvelope<OptionItemDto[]>> {
  return httpClient.get<OptionItemDto[]>(`/system/dicts/${dictCode}/options`, undefined, signal);
}

export function getDictItemsPage(
  dictTypeId: string,
  query: DictItemQuery = {},
  signal?: AbortSignal
): Promise<ApiEnvelope<GridPageResult<DictItemListItemDto>>> {
  return queryViewGrid(
    'system_dict_item',
    queryViewRequest(query.pageIndex, query.pageSize, [
      optionalCondition('dictTypeId', 'eq', dictTypeId)
    ], query.filters ?? [], query.sorts?.length ? query.sorts : [{ direction: 'asc', field: 'sortOrder' }]),
    mapDictItem,
    signal
  );
}

export function getDictItems(dictTypeId: string): Promise<ApiEnvelope<DictItemListItemDto[]>> {
  return getDictItemsPage(dictTypeId, { pageIndex: 1, pageSize: 5000, sorts: [{ direction: 'asc', field: 'sortOrder' }] }).then((response) => ({
    ...response,
    data: response.data.items
  }));
}

export function createDictItem(
  dictTypeId: string,
  request: DictItemUpsertRequest
): Promise<ApiEnvelope<DictItemListItemDto>> {
  return httpClient.post<DictItemListItemDto, DictItemUpsertRequest>(`/system/dicts/types/${dictTypeId}/items`, request);
}

export function updateDictItem(id: string, request: DictItemUpsertRequest): Promise<ApiEnvelope<DictItemListItemDto>> {
  return httpClient.put<DictItemListItemDto, DictItemUpsertRequest>(`/system/dicts/items/${id}`, request);
}

export function getDictItem(id: string): Promise<ApiEnvelope<DictItemListItemDto>> {
  return httpClient.get<DictItemListItemDto>(`/system/dicts/items/${id}`);
}

export function deleteDictItem(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/system/dicts/items/${id}`);
}
