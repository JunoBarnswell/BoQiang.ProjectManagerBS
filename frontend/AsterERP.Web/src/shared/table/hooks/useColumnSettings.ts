import { type Dispatch, type SetStateAction, useCallback, useEffect, useMemo, useRef, useState } from 'react';

import type { DataTableColumn, DataTableColumnSetting } from '../tableTypes';

export interface TableDisplaySettings {
  showVerticalLines?: boolean;
}

interface UseColumnSettingsOptions<TItem> {
  columnSettingsKey?: string;
  columns: DataTableColumn<TItem>[];
  externalColumnSettings?: DataTableColumnSetting[];
  onColumnSettingsChange?: (columns: DataTableColumnSetting[]) => void;
  onRefreshColumns?: (columns: DataTableColumnSetting[]) => void;
}

interface UseColumnSettingsResult {
  columnSettings: DataTableColumnSetting[];
  reorderColumn: (sourceKey: string, targetKey: string) => void;
  resetColumnSettings: () => void;
  setColumnSettings: Dispatch<SetStateAction<DataTableColumnSetting[]>>;
  storageKey: string;
  updateColumnSetting: (key: string, patch: Partial<DataTableColumnSetting>) => void;
}

export function safeStorageKey(key?: string): string {
  const normalized = (key ?? '').trim();
  return `astererp.table.columns.${normalized.length > 0 ? normalized : 'default'}`;
}

export function safeDisplayStorageKey(key?: string): string {
  return `${safeStorageKey(key)}.display`;
}

export function loadPersistedDisplaySettings(key: string): TableDisplaySettings {
  if (typeof window === 'undefined') {
    return {};
  }

  try {
    const raw = window.localStorage.getItem(key);
    if (!raw) {
      return {};
    }

    const parsed = JSON.parse(raw) as TableDisplaySettings;
    return {
      showVerticalLines: parsed.showVerticalLines === true
    };
  } catch {
    return {};
  }
}

export function savePersistedDisplaySettings(key: string, settings: TableDisplaySettings): void {
  if (typeof window === 'undefined') {
    return;
  }

  try {
    window.localStorage.setItem(key, JSON.stringify(settings));
  } catch {
    // noop
  }
}

export function parseWidthPx(value?: string): number {
  const parsed = Number.parseFloat(String(value ?? '').trim());
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 140;
}

export function flattenLeafColumns<TItem>(columns: DataTableColumn<TItem>[]): DataTableColumn<TItem>[] {
  return columns.flatMap((column) => column.children?.length ? flattenLeafColumns(column.children) : [column]);
}

function loadPersistedSettings(key: string): DataTableColumnSetting[] {
  if (typeof window === 'undefined') {
    return [];
  }

  try {
    const raw = window.localStorage.getItem(key);
    if (!raw) {
      return [];
    }

    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed.filter((item) => typeof (item as { key?: unknown })?.key === 'string') as DataTableColumnSetting[];
  } catch {
    return [];
  }
}

function savePersistedSettings(key: string, settings: DataTableColumnSetting[]): void {
  if (typeof window === 'undefined') {
    return;
  }

  try {
    window.localStorage.setItem(key, JSON.stringify(settings));
  } catch {
    // noop
  }
}

function removePersistedSettings(key: string): void {
  if (typeof window === 'undefined') {
    return;
  }

  try {
    window.localStorage.removeItem(key);
  } catch {
    // noop
  }
}

function stableStringify(value: unknown): string {
  return JSON.stringify(value ?? null);
}

function isSameColumnSettings(left: DataTableColumnSetting[], right: DataTableColumnSetting[]): boolean {
  if (left.length !== right.length) {
    return false;
  }

  return left.every((item, index) => {
    const next = right[index];
    return (
      item.key === next.key &&
      item.binding === next.binding &&
      item.fixed === next.fixed &&
      item.isVisible === next.isVisible &&
      item.order === next.order &&
      item.queryField === next.queryField &&
      item.renderer === next.renderer &&
      item.sortField === next.sortField &&
      item.title === next.title &&
      item.width === next.width &&
      stableStringify(item.merge) === stableStringify(next.merge) &&
      stableStringify(item.valueSource) === stableStringify(next.valueSource)
    );
  });
}

function buildDefaultColumnSettings<TItem>(columns: DataTableColumn<TItem>[]): DataTableColumnSetting[] {
  return flattenLeafColumns(columns).map((column, index) => ({
    binding: column.binding,
    fixed: column.fixed,
    isVisible: column.isVisible ?? true,
    key: column.key,
    order: column.order ?? index,
    title: typeof column.title === 'string' ? column.title : undefined,
    width: column.width
  }));
}

function buildColumnSettings<TItem>(
  columns: DataTableColumn<TItem>[],
  storageKey: string,
  previous: DataTableColumnSetting[],
  externalSettings?: DataTableColumnSetting[]
): DataTableColumnSetting[] {
  const persisted = loadPersistedSettings(storageKey);
  const persistedMap = new Map(persisted.map((item) => [item.key, item]));
  const previousMap = new Map(previous.map((item) => [item.key, item]));
  const externalMap = new Map((externalSettings ?? []).map((item) => [item.key, item]));

  const merged = flattenLeafColumns(columns).map((column, index) => {
    const externalItem = externalMap.get(column.key);
    const persistedItem = persistedMap.get(column.key);
    const previousItem = previousMap.get(column.key);

    return {
      binding: externalItem?.binding ?? persistedItem?.binding ?? previousItem?.binding ?? column.binding,
      fixed: externalItem?.fixed ?? persistedItem?.fixed ?? previousItem?.fixed ?? column.fixed,
      isVisible: externalItem?.isVisible ?? persistedItem?.isVisible ?? previousItem?.isVisible ?? (column.isVisible ?? true),
      key: column.key,
      merge: externalItem?.merge ?? persistedItem?.merge ?? previousItem?.merge,
      order: externalItem?.order ?? persistedItem?.order ?? previousItem?.order ?? column.order ?? index,
      queryField: externalItem?.queryField ?? persistedItem?.queryField ?? previousItem?.queryField,
      renderer: externalItem?.renderer ?? persistedItem?.renderer ?? previousItem?.renderer,
      sortField: externalItem?.sortField ?? persistedItem?.sortField ?? previousItem?.sortField,
      title: externalItem?.title ?? persistedItem?.title ?? previousItem?.title ?? (typeof column.title === 'string' ? column.title : undefined),
      valueSource: externalItem?.valueSource ?? persistedItem?.valueSource ?? previousItem?.valueSource,
      width: externalItem?.width ?? persistedItem?.width ?? previousItem?.width ?? column.width
    };
  });

  return merged.sort((left, right) => (left.order ?? 0) - (right.order ?? 0));
}

export function useColumnSettings<TItem>({
  columnSettingsKey,
  columns,
  externalColumnSettings,
  onColumnSettingsChange,
  onRefreshColumns
}: UseColumnSettingsOptions<TItem>): UseColumnSettingsResult {
  const storageKey = useMemo(() => safeStorageKey(columnSettingsKey), [columnSettingsKey]);
  const skipNextPersistRef = useRef(false);
  const [columnSettings, setColumnSettings] = useState<DataTableColumnSetting[]>(() =>
    buildColumnSettings(columns, storageKey, [], externalColumnSettings)
  );

  useEffect(() => {
    setColumnSettings((current) => {
      const next = buildColumnSettings(columns, storageKey, current, externalColumnSettings);
      return isSameColumnSettings(current, next) ? current : next;
    });
  }, [columns, externalColumnSettings, storageKey]);

  useEffect(() => {
    if (!externalColumnSettings) {
      if (skipNextPersistRef.current) {
        skipNextPersistRef.current = false;
      } else {
        savePersistedSettings(storageKey, columnSettings);
      }
    }

    onRefreshColumns?.(columnSettings);
    onColumnSettingsChange?.(columnSettings);
  }, [columnSettings, externalColumnSettings, onColumnSettingsChange, onRefreshColumns, storageKey]);

  const updateColumnSetting = useCallback((key: string, patch: Partial<DataTableColumnSetting>) => {
    setColumnSettings((current) =>
      current.map((item) => item.key === key ? { ...item, ...patch } : item)
    );
  }, []);

  const reorderColumn = useCallback((sourceKey: string, targetKey: string) => {
    if (sourceKey === targetKey) {
      return;
    }

    setColumnSettings((current) => {
      const sorted = [...current].sort((left, right) => (left.order ?? 0) - (right.order ?? 0));
      const sourceIndex = sorted.findIndex((item) => item.key === sourceKey);
      const targetIndex = sorted.findIndex((item) => item.key === targetKey);
      if (sourceIndex < 0 || targetIndex < 0) {
        return current;
      }

      const next = [...sorted];
      const [moved] = next.splice(sourceIndex, 1);
      next.splice(targetIndex, 0, moved);

      return next.map((item, itemIndex) => ({
        ...item,
        order: itemIndex
      }));
    });
  }, []);

  const resetColumnSettings = useCallback(() => {
    skipNextPersistRef.current = true;
    removePersistedSettings(storageKey);
    setColumnSettings(buildDefaultColumnSettings(columns));
  }, [columns, storageKey]);

  return {
    columnSettings,
    reorderColumn,
    resetColumnSettings,
    setColumnSettings,
    storageKey,
    updateColumnSetting
  };
}
