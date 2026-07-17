import type { ReactNode } from 'react';

import type { FormFieldConfig } from '../../forms/formTypes';
import type { ResponsiveGridColumns } from '../../responsive/ResponsiveFormGrid';
import type { ResponsiveToolbarAction } from '../../responsive/ResponsiveToolbar';
import type { DataTableProps } from '../../table/tableTypes';

export interface AdvancedDataGridSearchProps<TValues extends object> {
  columns?: ResponsiveGridColumns;
  defaultCollapsed?: boolean;
  fields: FormFieldConfig<TValues>[];
  loading?: boolean;
  maxRows?: number;
  onReset?: () => void;
  onSubmit: (value: TValues) => void;
  onValueChange: (value: TValues) => void;
  value: TValues;
}

export interface AdvancedDataGridToolbarProps {
  actions?: ResponsiveToolbarAction[];
  title?: ReactNode;
}

export interface AdvancedDataGridTableProps<TItem> extends DataTableProps<TItem> {
  reservedHeight?: number;
}

export interface AdvancedDataGridProps<TItem, TSearchValues extends object> {
  actions?: ReactNode;
  children?: ReactNode;
  className?: string;
  description?: ReactNode;
  editor?: ReactNode;
  eyebrow?: ReactNode;
  fitScreen?: boolean;
  footer?: ReactNode;
  search?: AdvancedDataGridSearchProps<TSearchValues>;
  sidePanel?: ReactNode;
  table: AdvancedDataGridTableProps<TItem>;
  title: ReactNode;
  toolbar?: AdvancedDataGridToolbarProps;
}
