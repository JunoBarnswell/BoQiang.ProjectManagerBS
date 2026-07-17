import { QueryClient } from '@tanstack/react-query';
import { describe, expect, it, vi } from 'vitest';

import { queryKeys } from '../../core/query/queryKeys';

function createDeferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((nextResolve) => {
    resolve = nextResolve;
  });

  return { promise, resolve };
}

describe('dict query cache', () => {
  it('dedupes concurrent requests for the same dict code', async () => {
    const queryClient = new QueryClient({
      defaultOptions: {
        queries: {
          retry: false
        }
      }
    });
    const deferred = createDeferred<string[]>();
    const queryFn = vi.fn(() => deferred.promise);
    const queryKey = queryKeys.dict.byType('sys_enabled_status');

    const firstRequest = queryClient.fetchQuery({ queryFn, queryKey });
    const secondRequest = queryClient.fetchQuery({ queryFn, queryKey });

    expect(queryFn).toHaveBeenCalledTimes(1);
    deferred.resolve(['1', '0']);

    await expect(firstRequest).resolves.toEqual(['1', '0']);
    await expect(secondRequest).resolves.toEqual(['1', '0']);
    queryClient.clear();
  });
});
