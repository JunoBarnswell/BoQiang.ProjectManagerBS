import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import type { DataTableSortDirection, DataTableSortRule } from '../tableTypes';

interface UseTableSortRulesOptions {
  onSortChange?: (sortKey: string, direction: DataTableSortDirection | null) => void;
  onSortsChange?: (sorts: DataTableSortRule[]) => void;
  sorts?: DataTableSortRule[];
}

interface UseTableSortRulesResult {
  addSortRule: (field: string) => void;
  clearSortRules: () => void;
  moveSortRule: (index: number, offset: -1 | 1) => void;
  primarySortRule: DataTableSortRule | null;
  removeSortRule: (index: number) => void;
  setSortRuleDirection: (index: number, direction: DataTableSortDirection) => void;
  setSortRuleField: (index: number, field: string) => void;
  sortRules: DataTableSortRule[];
  toggleSort: (field: string) => void;
}

function normalizeDirection(direction: DataTableSortDirection | null | undefined): DataTableSortDirection | null {
  return direction === 'asc' || direction === 'desc' ? direction : null;
}

function normalizeSortRules(sorts?: DataTableSortRule[]): DataTableSortRule[] {
  const seen = new Set<string>();
  const result: DataTableSortRule[] = [];

  for (const sort of sorts ?? []) {
    const field = String(sort.field ?? '').trim();
    const direction = normalizeDirection(sort.direction);
    if (!field || !direction || seen.has(field)) {
      continue;
    }

    seen.add(field);
    result.push({ field, direction });
  }

  return result;
}

function getNextDirection(current?: DataTableSortRule): DataTableSortDirection | null {
  if (!current) {
    return 'asc';
  }

  if (current.direction === 'asc') {
    return 'desc';
  }

  return null;
}

function moveRule(rules: DataTableSortRule[], index: number, offset: -1 | 1): DataTableSortRule[] {
  const nextIndex = index + offset;
  if (index < 0 || nextIndex < 0 || nextIndex >= rules.length) {
    return rules;
  }

  const next = [...rules];
  const [moved] = next.splice(index, 1);
  next.splice(nextIndex, 0, moved);
  return next;
}

function dedupeRules(rules: DataTableSortRule[]): DataTableSortRule[] {
  return normalizeSortRules(rules);
}

export function useTableSortRules({
  onSortChange,
  onSortsChange,
  sorts
}: UseTableSortRulesOptions): UseTableSortRulesResult {
  const [sortRules, setSortRules] = useState<DataTableSortRule[]>(() => normalizeSortRules(sorts));
  const sortRulesRef = useRef(sortRules);
  const lastPrimaryFieldRef = useRef(sortRules[0]?.field ?? '');

  useEffect(() => {
    if (sorts) {
      const nextRules = normalizeSortRules(sorts);
      sortRulesRef.current = nextRules;
      setSortRules(nextRules);
    }
  }, [sorts]);

  const notify = useCallback(
    (nextRules: DataTableSortRule[], fallbackField = '') => {
      const primary = nextRules[0];
      lastPrimaryFieldRef.current = primary?.field ?? fallbackField ?? lastPrimaryFieldRef.current;
      onSortsChange?.(nextRules);
      onSortChange?.(primary?.field ?? lastPrimaryFieldRef.current, primary?.direction ?? null);
    },
    [onSortChange, onSortsChange]
  );

  const commit = useCallback(
    (updater: (current: DataTableSortRule[]) => DataTableSortRule[], fallbackField = '') => {
      const next = dedupeRules(updater(sortRulesRef.current));
      sortRulesRef.current = next;
      setSortRules(next);
      notify(next, fallbackField);
    },
    [notify]
  );

  const toggleSort = useCallback(
    (field: string) => {
      const normalizedField = String(field ?? '').trim();
      if (!normalizedField) {
        return;
      }

      commit((current) => {
        const currentRule = current.find((rule) => rule.field === normalizedField);
        const nextDirection = getNextDirection(currentRule);
        const withoutCurrent = current.filter((rule) => rule.field !== normalizedField);

        if (!nextDirection) {
          return withoutCurrent;
        }

        return [{ field: normalizedField, direction: nextDirection }, ...withoutCurrent];
      }, normalizedField);
    },
    [commit]
  );

  const addSortRule = useCallback(
    (field: string) => {
      const normalizedField = String(field ?? '').trim();
      if (!normalizedField) {
        return;
      }

      commit((current) => {
        if (current.some((rule) => rule.field === normalizedField)) {
          return current;
        }

        return [...current, { field: normalizedField, direction: 'asc' }];
      }, normalizedField);
    },
    [commit]
  );

  const removeSortRule = useCallback(
    (index: number) => {
      commit((current) => current.filter((_, currentIndex) => currentIndex !== index));
    },
    [commit]
  );

  const moveSortRule = useCallback(
    (index: number, offset: -1 | 1) => {
      commit((current) => moveRule(current, index, offset));
    },
    [commit]
  );

  const setSortRuleField = useCallback(
    (index: number, field: string) => {
      const normalizedField = String(field ?? '').trim();
      if (!normalizedField) {
        return;
      }

      commit((current) =>
        current.map((rule, currentIndex) => currentIndex === index ? { ...rule, field: normalizedField } : rule)
      , normalizedField);
    },
    [commit]
  );

  const setSortRuleDirection = useCallback(
    (index: number, direction: DataTableSortDirection) => {
      const normalizedDirection = normalizeDirection(direction);
      if (!normalizedDirection) {
        return;
      }

      commit((current) =>
        current.map((rule, currentIndex) => currentIndex === index ? { ...rule, direction: normalizedDirection } : rule)
      );
    },
    [commit]
  );

  const clearSortRules = useCallback(() => {
    commit(() => []);
  }, [commit]);

  const primarySortRule = useMemo(() => sortRules[0] ?? null, [sortRules]);

  return {
    addSortRule,
    clearSortRules,
    moveSortRule,
    primarySortRule,
    removeSortRule,
    setSortRuleDirection,
    setSortRuleField,
    sortRules,
    toggleSort
  };
}
