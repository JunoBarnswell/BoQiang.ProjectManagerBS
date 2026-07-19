import type { ReactNode } from 'react';

import type { MenuTreeNodeDto } from '../../api/system/system.types';
import type { BreakpointName } from '../../core/responsive/breakpoint';

export type FormFieldType =
  | 'checkbox'
  | 'date'
  | 'datetime-local'
  | 'dict'
  | 'multiselect'
  | 'number'
  | 'permissionTree'
  | 'range'
  | 'select'
  | 'switch'
  | 'textarea'
  | 'text';

export interface FormOption {
  disabled?: boolean;
  label: string;
  value: string;
}

export interface FormFieldConfig<TValues extends object> {
  dictType?: string;
  colSpan?: number | Partial<Record<BreakpointName, number>>;
  disabled?: boolean;
  emptyOptionLabel?: string;
  helpText?: string;
  hideBelow?: BreakpointName;
  label: string;
  max?: number;
  min?: number;
  name: keyof TValues & string;
  options?: FormOption[];
  permissionTreeNodes?: MenuTreeNodeDto[];
  placeholder?: string;
  required?: boolean;
  rows?: number;
  section?: string;
  span?: number;
  step?: number;
  type: FormFieldType;
}

export interface FormFieldRendererProps<TValues extends object> {
  field: FormFieldConfig<TValues>;
  onValueChange: (name: keyof TValues & string, value: TValues[keyof TValues & string]) => void;
  translate: (key: string) => string;
  value: TValues[keyof TValues & string];
}

export interface FormActionConfig {
  disabled?: boolean;
  label: string;
  loading?: boolean;
  onClick: () => void;
  type?: 'button' | 'reset' | 'submit';
  variant?: 'ghost' | 'primary';
}

export interface FormSectionConfig {
  description?: string;
  title: string;
  children: ReactNode;
}
