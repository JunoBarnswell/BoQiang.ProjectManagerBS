import type { QueryKey, UseQueryOptions } from '@tanstack/react-query';

import type { GridPageResult } from '../../api/shared.types';
import type { ApiEnvelope } from '../http/apiEnvelope';

import { useApiQuery } from './useApiQuery';

export type PagedQuery<TSearch extends object> = TSearch & {
  pageIndex: number;
  pageSize: number;
};

interface UsePagedQueryOptions<TItem, TSearch extends object, TError = Error, TData = GridPageResult<TItem>> {
  enabled?: boolean;
  keepPreviousData?: boolean;
  query: PagedQuery<TSearch>;
  queryFn: (query: PagedQuery<TSearch>, signal?: AbortSignal) => Promise<ApiEnvelope<GridPageResult<TItem>>>;
  queryKeyPrefix: QueryKey;
  select?: UseQueryOptions<GridPageResult<TItem>, TError, TData>['select'];
  staleTimeMs?: number;
}

export function usePagedQuery<TItem, TSearch extends object, TError = Error, TData = GridPageResult<TItem>>(
  options: UsePagedQueryOptions<TItem, TSearch, TError, TData>
) {
  return useApiQuery<GridPageResult<TItem>, TError, TData>({
    enabled: options.enabled,
    keepPreviousData: options.keepPreviousData ?? true,
    queryFn: async ({ signal }) => {
      const response = await options.queryFn(options.query, signal);
      return response.data;
    },
    queryKey: [...options.queryKeyPrefix, options.query],
    select: options.select,
    staleTimeMs: options.staleTimeMs
  });
}
