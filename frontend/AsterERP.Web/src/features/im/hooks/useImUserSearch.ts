import { useCallback, useState } from 'react';

import { useImContext } from '../components/ImProvider';
import type { ImUserSearchItem } from '../types/imTypes';

export function useImUserSearch() {
  const { adapter } = useImContext();
  const [loading, setLoading] = useState(false);
  const [users, setUsers] = useState<ImUserSearchItem[]>([]);

  const search = useCallback(async (keyword: string, signal?: AbortSignal) => {
    if (!keyword.trim()) {
      setUsers([]);
      return [];
    }

    setLoading(true);
    try {
      const result = await adapter.searchUsers(keyword, signal);
      setUsers(result);
      return result;
    } finally {
      setLoading(false);
    }
  }, [adapter]);

  return { loading, search, users };
}
