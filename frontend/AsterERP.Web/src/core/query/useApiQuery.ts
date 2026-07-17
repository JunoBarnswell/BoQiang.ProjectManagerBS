import { keepPreviousData, useQuery, type QueryKey, type UseQueryOptions } from '@tanstack/react-query';

export interface ApiQueryContext {
  signal: AbortSignal;
}

interface ApiQueryOptions<TQueryFnData, TError, TData> {
  enabled?: boolean;
  gcTimeMs?: number;
  keepPreviousData?: boolean;
  placeholderData?: UseQueryOptions<TQueryFnData, TError, TData>['placeholderData'];
  queryFn: (context: ApiQueryContext) => Promise<TQueryFnData>;
  queryKey: QueryKey;
  refetchOnMount?: UseQueryOptions<TQueryFnData, TError, TData>['refetchOnMount'];
  refetchOnReconnect?: UseQueryOptions<TQueryFnData, TError, TData>['refetchOnReconnect'];
  retry?: UseQueryOptions<TQueryFnData, TError, TData>['retry'];
  select?: UseQueryOptions<TQueryFnData, TError, TData>['select'];
  staleTimeMs?: number;
}

export function useApiQuery<TQueryFnData, TError = Error, TData = TQueryFnData>(
  options: ApiQueryOptions<TQueryFnData, TError, TData>
) {
  return useQuery<TQueryFnData, TError, TData>({
    enabled: options.enabled ?? true,
    gcTime: options.gcTimeMs,
    placeholderData: options.keepPreviousData ? keepPreviousData : options.placeholderData,
    queryFn: ({ signal }) => options.queryFn({ signal }),
    queryKey: options.queryKey,
    refetchOnMount: options.refetchOnMount,
    refetchOnReconnect: options.refetchOnReconnect,
    retry: options.retry,
    select: options.select,
    staleTime: options.staleTimeMs
  });
}
