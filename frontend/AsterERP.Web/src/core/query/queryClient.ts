import { QueryClient } from '@tanstack/react-query';

import { isHttpError } from '../http/httpError';

import { QUERY_STALE_TIME_MS } from './cacheDurations';

function shouldRetryQuery(failureCount: number, error: unknown): boolean {
  if (isHttpError(error) && error.status >= 400 && error.status < 500) {
    return false;
  }

  return failureCount < 1;
}

export const queryClient = new QueryClient({
  defaultOptions: {
    mutations: {
      retry: 0
    },
    queries: {
      gcTime: 5 * 60 * 1000,
      refetchOnWindowFocus: false,
      retry: shouldRetryQuery,
      staleTime: QUERY_STALE_TIME_MS
    }
  }
});
