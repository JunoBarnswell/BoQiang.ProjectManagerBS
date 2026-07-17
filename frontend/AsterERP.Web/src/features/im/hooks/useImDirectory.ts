import { useCallback, useEffect, useState } from 'react';

import { useImContext } from '../components/ImProvider';
import type { ImDirectory } from '../types/imTypes';

const emptyDirectory: ImDirectory = { departments: [] };

export function useImDirectory(keyword: string) {
  const { adapter } = useImContext();
  const [directory, setDirectory] = useState<ImDirectory>(emptyDirectory);
  const [loading, setLoading] = useState(false);

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    try {
      const next = await adapter.getDirectory(keyword, signal);
      setDirectory(next);
    } finally {
      if (!signal?.aborted) {
        setLoading(false);
      }
    }
  }, [adapter, keyword]);

  useEffect(() => {
    const controller = new AbortController();
    const timer = window.setTimeout(() => void load(controller.signal), 250);
    return () => {
      window.clearTimeout(timer);
      controller.abort();
    };
  }, [load]);

  return { directory, loading, refresh: load };
}
