import type { ApplicationDataCenterResourcePath } from '../../../../api/application-data-center/applicationDataCenter.api';
import type { ApplicationDataCenterModuleKey } from '../../../../api/application-data-center/applicationDataCenter.types';

export type ConfigFormMode = 'create' | 'edit' | 'view';

export type ConfigFieldTarget = 'config' | 'secret';

export type ConfigFieldComponent =
  | 'text'
  | 'password'
  | 'number'
  | 'select'
  | 'switch'
  | 'textarea'
  | 'objectSelect'
  | 'dataSourceSelect'
  | 'modelSelect'
  | 'userSelect'
  | 'riskFieldSelect'
  | 'tableSelect'
  | 'keyValueList'
  | 'mappingList'
  | 'fieldList'
  | 'codeRuleSegments';

export interface ConfigOption {
  label: string;
  value: string;
}

export interface ConfigCondition {
  field: string;
  equals?: unknown;
  notEquals?: unknown;
  includes?: unknown[];
}

export type ConfigValidationRule =
  | { type: 'required'; message?: string }
  | { type: 'url'; message?: string }
  | { type: 'numberRange'; min?: number; max?: number; message?: string }
  | { type: 'jsonObject'; message?: string };

export interface ConfigFieldSchema {
  component: ConfigFieldComponent;
  defaultValue?: unknown;
  helpText?: string;
  label: string;
  name: string;
  options?: ConfigOption[];
  objectResourcePath?: ApplicationDataCenterResourcePath;
  objectResourcePaths?: ApplicationDataCenterResourcePath[];
  objectTypes?: string[];
  placeholder?: string;
  required?: boolean;
  rows?: number;
  sensitive?: boolean;
  span?: 1 | 2;
  tableDataSourceField?: string;
  target?: ConfigFieldTarget;
  validation?: ConfigValidationRule[];
  visibleWhen?: ConfigCondition;
}

export interface ConfigSectionSchema {
  collapsible?: boolean;
  defaultCollapsed?: boolean;
  description?: string;
  fields: ConfigFieldSchema[];
  key: string;
  title: string;
}

export interface ConfigFormSchema {
  description: string;
  moduleKey: ApplicationDataCenterModuleKey;
  nextActionHints?: string[];
  objectType: string;
  sections: ConfigSectionSchema[];
  title: string;
}

export type ConfigFormValues = Record<string, unknown>;

export interface ConfigParseResult {
  error?: string;
  value: Record<string, unknown>;
}

export interface ConfigSecretState {
  hasExistingSecret: boolean;
  isCleared: boolean;
  isDirty: boolean;
}

export interface ConfigFieldError {
  field: string;
  message: string;
}

export interface ConfigNormalizeOptions {
  isEdit: boolean;
}
