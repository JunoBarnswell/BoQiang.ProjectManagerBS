import { useCallback, useEffect, useRef, useState, type Dispatch, type SetStateAction } from 'react';
import { useLocation } from 'react-router-dom';

import { useTabStore, type TabStoreState } from '../../core/state';

export function useResolvedTabCacheKey(cacheKey?: string) {
  const location = useLocation();

  if (!cacheKey) {
    return `${location.pathname}::default`;
  }

  if (cacheKey.startsWith('/')) {
    return cacheKey;
  }

  return `${location.pathname}::${cacheKey}`;
}

export function useTabCacheStore<T>(selector: (state: TabStoreState) => T) {
  return useTabStore(selector);
}

export function useTabPageState<T>(
  initialState: T,
  options?: {
    cacheKey?: string;
  }
): [T, Dispatch<SetStateAction<T>>, () => void] {
  const resolvedCacheKey = useResolvedTabCacheKey(options?.cacheKey);
  const getPageCache = useTabStore((state) => state.getPageCache);
  const setPageCache = useTabStore((state) => state.setPageCache);
  const clearPageCache = useTabStore((state) => state.clearPageCache);
  const initialRef = useRef<T | null>(null);

  if (initialRef.current === null) {
    const cached = getPageCache<T>(resolvedCacheKey);
    initialRef.current = cached ?? initialState;
  }

  const [state, setState] = useState<T>(initialRef.current);

  useEffect(() => {
    setPageCache(resolvedCacheKey, state);
  }, [resolvedCacheKey, setPageCache, state]);

  const clearState = useCallback(() => {
    clearPageCache(resolvedCacheKey);
    setState(initialState);
  }, [clearPageCache, initialState, resolvedCacheKey]);

  return [state, setState, clearState];
}
