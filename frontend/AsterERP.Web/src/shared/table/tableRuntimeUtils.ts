import type { CSSProperties } from 'react';

import type { ColumnSettingsPanelColumn } from './column-settings/ColumnSettingsPanel';
import { parseWidthPx, flattenLeafColumns } from './hooks/useColumnSettings';
import { getColumnSortField, isColumnSortable } from './tableFilterUtils';
import { titleToText, type RenderColumn } from './tableLayoutUtils';
import {
  isBetweenReady,
  isQueryValueReady,
  normalizeDate,
  normalizeNumber,
  normalizeNumberOrText
} from './tableQueryUtils';
import type {
  DataTableColumn,
  DataTableColumnSetting,
  DataTableCondition,
  DataTableFilterType
} from './tableTypes';

export const DEFAULT_PAGE_SIZE_OPTIONS = [10, 20, 50, 100];

export function moveArrayItem<TItem>(items: TItem[], sourceIndex: number, targetIndex: number): TItem[] {
  if (sourceIndex === targetIndex || sourceIndex < 0 || targetIndex < 0 || sourceIndex >= items.length || targetIndex >= items.length) {
    return items;
  }

  const next = [...items];
  const [moved] = next.splice(sourceIndex, 1);
  next.splice(targetIndex, 0, moved);
  return next;
}

export function buildRuntimeColumns<TItem>(
  columns: DataTableColumn<TItem>[],
  columnSettings: DataTableColumnSetting[]
): RenderColumn<TItem>[] {
  const overrideMap = new Map(columnSettings.map((item) => [item.key, item]));

  return flattenLeafColumns(columns)
    .map((column, index) => {
      const override = overrideMap.get(column.key);
      const canHide = column.canHide !== false;
      const canFreeze = column.canFreeze !== false;
      const title = override?.title ?? column.title;
      const binding = override?.binding ?? column.binding;
      const filterField = override?.queryField ?? column.filterField;
      const sortField = override?.sortField ?? column.sortField;
      return {
        column: { ...column, binding, filterField, sortField, title },
        fixed: canFreeze ? override?.fixed : column.fixed,
        isVisible: canHide ? override?.isVisible !== false : true,
        order: override?.order ?? (column.order ?? index),
        widthPx: parseWidthPx(override?.width ?? column.width)
      };
    })
    .sort((left, right) => (left.order ?? 0) - (right.order ?? 0));
}

export function buildColumnSettingsPanelColumns<TItem>(
  columnsRuntime: RenderColumn<TItem>[],
  columnSettingMap: Map<string, DataTableColumnSetting>,
  isRemoteSorting: boolean
): ColumnSettingsPanelColumn[] {
  return columnsRuntime.map((column) => ({
    canFreeze: column.column.canFreeze !== false,
    canHide: column.column.canHide !== false,
    fixed: column.fixed,
    isVisible: column.isVisible,
    key: column.column.key,
    setting: columnSettingMap.get(column.column.key),
    sortable: isColumnSortable(column.column, isRemoteSorting),
    sortField: getColumnSortField(column.column),
    title: column.column.title,
    titleText: titleToText(column.column.title, column.column.key),
    width: columnSettingMap.get(column.column.key)?.width ?? column.column.width,
    widthPx: column.widthPx
  }));
}

export function getDataTableCellStyle<TItem>(
  column: RenderColumn<TItem>,
  leftOffsets: Map<string, number>,
  rightOffsets: Map<string, number>,
  isHeader = false
): CSSProperties {
  const widthPx = `${column.widthPx}px`;
  const base: CSSProperties = {
    width: widthPx,
    minWidth: widthPx,
    maxWidth: widthPx
  };

  if (isHeader) {
    base.top = 0;
    base.position = 'sticky';
    base.zIndex = 3;
  }

  if (column.fixed === 'left') {
    return {
      ...base,
      background: isHeader ? '#f8fafc' : '#ffffff',
      left: `${leftOffsets.get(column.column.key) ?? 0}px`,
      position: 'sticky',
      right: undefined,
      zIndex: isHeader ? 4 : 2
    };
  }

  if (column.fixed === 'right') {
    return {
      ...base,
      background: isHeader ? '#f8fafc' : '#ffffff',
      position: 'sticky',
      right: `${rightOffsets.get(column.column.key) ?? 0}px`,
      left: undefined,
      zIndex: isHeader ? 4 : 2
    };
  }

  return base;
}

export function getDataTableStickyClass<TItem>(column: RenderColumn<TItem>): string {
  if (column.fixed === 'left') {
    return "sticky z-[2] shadow-[4px_0_0_-2px_var(--app-border)] backdrop-blur-[16px]";
  }

  if (column.fixed === 'right') {
    return "sticky z-[2] shadow-[-4px_0_0_-2px_var(--app-border)] backdrop-blur-[16px]";
  }

  return '';
}

export function isDataTableConditionReady(condition: DataTableCondition, fieldType: DataTableFilterType): boolean {
  if (!condition.field) {
    return false;
  }

  if (condition.operator === 'between') {
    return isBetweenReady(condition.value, condition.valueTo);
  }

  if (fieldType === 'number') {
    return normalizeNumber(condition.value) !== null;
  }

  if (fieldType === 'date') {
    return normalizeDate(condition.value) !== null;
  }

  return isQueryValueReady(condition.value);
}

export function normalizeHeaderFilterCondition(headerFilterDraft: DataTableCondition, field: string): DataTableCondition {
  return {
    ...headerFilterDraft,
    field,
    value: normalizeNumberOrText(headerFilterDraft.value),
    valueTo: headerFilterDraft.operator === 'between' ? normalizeNumberOrText(headerFilterDraft.valueTo) : undefined
  };
}
