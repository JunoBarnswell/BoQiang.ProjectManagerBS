import { keepPreviousData, useQueries, type QueryKey, type UseQueryOptions, type UseQueryResult } from '@tanstack/react-query';
import { useMemo } from 'react';

import type { ApiQueryContext } from './useApiQuery';

export interface ApiNamedQueryOptions<TQueryFnData, TError = Error, TData = TQueryFnData> {
  enabled?: boolean;
  keepPreviousData?: boolean;
  placeholderData?: UseQueryOptions<TQueryFnData, TError, TData>['placeholderData'];
  queryFn: (context: ApiQueryContext) => Promise<TQueryFnData>;
  queryKey: QueryKey;
  select?: UseQueryOptions<TQueryFnData, TError, TData>['select'];
  staleTimeMs?: number;
}

export type ApiQueriesConfig = Record<
  string,
  {
    enabled?: boolean;
    keepPreviousData?: boolean;
    placeholderData?: UseQueryOptions<unknown, Error, unknown>['placeholderData'];
    queryFn: (context: ApiQueryContext) => Promise<unknown>;
    queryKey: QueryKey;
    select?: UseQueryOptions<unknown, Error, unknown>['select'];
    staleTimeMs?: number;
  }
>;

type ApiQueryResults<TQueries extends ApiQueriesConfig> = {
  [TKey in keyof TQueries]: TQueries[TKey] extends { queryFn: () => Promise<infer TQueryFnData> }
    ? UseQueryResult<TQueryFnData, Error>
    : UseQueryResult<unknown, Error>;
};

export interface ApiQueriesResult<TQueries extends ApiQueriesConfig> {
  isAnyError: boolean;
  isAnyFetching: boolean;
  isAnyLoading: boolean;
  refetchAll: () => Promise<void>;
  results: ApiQueryResults<TQueries>;
}

export function useApiQueries<TQueries extends ApiQueriesConfig>(queries: TQueries): ApiQueriesResult<TQueries> {
  const entries = useMemo(() => Object.entries(queries), [queries]);
  const results = useQueries({
    queries: entries.map(([, query]) => ({
      enabled: query.enabled ?? true,
      placeholderData: query.keepPreviousData ? keepPreviousData : query.placeholderData,
      queryFn: ({ signal }) => query.queryFn({ signal }),
      queryKey: query.queryKey,
      select: query.select,
      staleTime: query.staleTimeMs
    }))
  }) as UseQueryResult<unknown, Error>[];

  const keyedResults = entries.reduce<Record<string, UseQueryResult<unknown, Error>>>((accumulator, [key], index) => {
    accumulator[key] = results[index];
    return accumulator;
  }, {}) as ApiQueryResults<TQueries>;

  return {
    isAnyError: results.some((result) => result.isError),
    isAnyFetching: results.some((result) => result.isFetching),
    isAnyLoading: results.some((result) => result.isLoading),
    refetchAll: async () => {
      await Promise.all(results.map((result) => result.refetch()));
    },
    results: keyedResults
  };
}
